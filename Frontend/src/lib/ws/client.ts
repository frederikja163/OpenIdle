import {
	classifyMessage,
	encodeRequest,
	readRequestId,
	type EventType,
	type RequestMap,
	type RequestType,
	type ServerEvent,
	type ServerEventOf
} from './protocol';
import { defaultWsUrl, readStoredWsUrl, resolveWsUrl, writeStoredWsUrl } from './ws-url';

const DEFAULT_REQUEST_TIMEOUT_MS = 10_000;
const DEFAULT_CONNECT_TIMEOUT_MS = 5_000;
const DEFAULT_RECONNECT_BASE_MS = 500;
const DEFAULT_RECONNECT_MAX_MS = 15_000;
const DEFAULT_RECONNECT_ATTEMPTS = 8;

export class WsError extends Error {}

/**
 * `reconnecting` covers the whole recovery window — the wait, the attempt and
 * the session replay — because every consumer wants the same thing from all
 * three: hold position and say so, rather than treat the drop as a logout.
 */
export type WsStatus = 'closed' | 'connecting' | 'open' | 'reconnecting';

/** Issues a request that bypasses the replay gate. Only session replay gets one. */
export type PrivilegedSend = <K extends RequestType>(
	type: K,
	payload: RequestMap[K]['payload']
) => Promise<RequestMap[K]['response']>;

/** Which way a frame crossed the socket. `out` is a request, `in` anything received. */
export type FrameDirection = 'in' | 'out';

export interface WsClientOptions {
	url: string;
	requestTimeoutMs?: number;
	connectTimeoutMs?: number;
	reconnectBaseMs?: number;
	reconnectMaxMs?: number;
	maxReconnectAttempts?: number;
	socketFactory?: (url: string) => WebSocket;
}

interface PendingRequest {
	resolve: (response: never) => void;
	reject: (error: WsError) => void;
	timer: ReturnType<typeof setTimeout>;
	/**
	 * Whether the frame actually reached a socket. A request still waiting on a
	 * connection or on the replay gate has not, and must survive the retirement
	 * of a socket it never rode — including the retirement its own connect
	 * triggers when it finds a half-closed one.
	 */
	sent: boolean;
}

export class WsClient {
	private url: string;
	private readonly requestTimeoutMs: number;
	private readonly connectTimeoutMs: number;
	private readonly reconnectBaseMs: number;
	private readonly reconnectMaxMs: number;
	private readonly maxReconnectAttempts: number;
	private readonly socketFactory: (url: string) => WebSocket;

	private socket: WebSocket | null = null;
	private connectPromise: Promise<void> | null = null;
	private nextRequestId = 1;
	private readonly pending = new Map<number, PendingRequest>();
	/**
	 * Ids the backend still owes a reply for, oldest first — deliberately not
	 * the same set as `pending`, which a local timeout abandons while the
	 * backend carries on working. See the error branch of handleMessage.
	 */
	private readonly outstanding: number[] = [];
	private readonly eventHandlers = new Map<string, Set<(event: ServerEvent) => void>>();
	private readonly anyEventHandlers = new Set<(event: ServerEvent) => void>();
	private readonly closeHandlers = new Set<() => void>();
	private readonly statusHandlers = new Set<(status: WsStatus) => void>();
	private readonly frameHandlers = new Set<(direction: FrameDirection, raw: string) => void>();

	/**
	 * Bumped every time a connection is retired, so a caller can tell whether the
	 * connection its result belongs to is still the current one.
	 *
	 * This exists because rejecting a pending request only *schedules* a
	 * microtask: a caller's `catch` therefore always runs after the close
	 * handlers that were meant to clean up after it, and would otherwise write
	 * the dead connection's failure over the fresh state they just reset.
	 */
	private connectionGeneration = 0;
	private statusValue: WsStatus = 'closed';
	private everConnected = false;
	private deliberatelyClosed = false;
	private reconnectAttempt = 0;
	private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
	private resumeSession: ((send: PrivilegedSend) => Promise<void>) | null = null;
	private resumeGate: Promise<void> | null = null;
	private releaseResumeGate: (() => void) | null = null;

	constructor(options: WsClientOptions) {
		this.url = options.url;
		this.requestTimeoutMs = options.requestTimeoutMs ?? DEFAULT_REQUEST_TIMEOUT_MS;
		this.connectTimeoutMs = options.connectTimeoutMs ?? DEFAULT_CONNECT_TIMEOUT_MS;
		this.reconnectBaseMs = options.reconnectBaseMs ?? DEFAULT_RECONNECT_BASE_MS;
		this.reconnectMaxMs = options.reconnectMaxMs ?? DEFAULT_RECONNECT_MAX_MS;
		this.maxReconnectAttempts = options.maxReconnectAttempts ?? DEFAULT_RECONNECT_ATTEMPTS;
		this.socketFactory = options.socketFactory ?? ((url) => new WebSocket(url));
	}

	get generation(): number {
		return this.connectionGeneration;
	}

	get status(): WsStatus {
		return this.statusValue;
	}

	get currentUrl(): string {
		return this.url;
	}

	/**
	 * Repoints the client at another backend. Goes through close()/reopen() rather
	 * than swapping the url under a live socket, because moving to a different
	 * server ends the session — the backend session *is* the connection — and that
	 * is a deliberate shutdown, not a fault for the reconnect loop to recover from.
	 *
	 * The instance survives, which is what matters to wireSession(): its handler
	 * registration happens once, so a replacement client would come up unwired.
	 */
	setUrl(url: string): void {
		if (url === this.url) {
			return;
		}
		this.close();
		this.url = url;
		this.reopen();
	}

	connect(): Promise<void> {
		const existing = this.socket;
		if (existing?.readyState === WebSocket.OPEN) {
			return Promise.resolve();
		}
		// CLOSING and CLOSED only. A socket the server started closing is still
		// assigned, and its connect promise is still the resolved one from when it
		// opened, so retiring it here is what lets a replacement be built rather
		// than a dead connection handed back. A socket that is merely CONNECTING is
		// the one this call is waiting for, not a corpse to replace.
		if (existing && existing.readyState > WebSocket.OPEN) {
			this.retire(existing);
		}
		if (this.connectPromise) {
			return this.connectPromise;
		}
		this.setStatus(this.everConnected ? 'reconnecting' : 'connecting');
		let socket: WebSocket;
		try {
			// Constructed outside the promise below, because a throw in an executor
			// runs before the memo is assigned: clearing it in there would be undone
			// by the assignment, and the rejection would then be handed to every
			// later request for the life of the page.
			socket = this.socketFactory(this.url);
		} catch (error) {
			this.setStatus('closed');
			return Promise.reject(error instanceof WsError ? error : new WsError(String(error)));
		}
		this.connectPromise = this.watchSocket(socket);
		return this.connectPromise;
	}

	private watchSocket(socket: WebSocket): Promise<void> {
		return new Promise((resolve, reject) => {
			this.socket = socket;
			// A socket whose TCP handshake completes but whose upgrade never does
			// stays in CONNECTING until the browser's own transport timeout, tens
			// of seconds away. Closing it routes the failure through the 'close'
			// listener like any other, so nothing else needs to know about it.
			const connectTimer = setTimeout(() => socket.close(), this.connectTimeoutMs);
			socket.addEventListener('open', () => {
				clearTimeout(connectTimer);
				const restoring = this.everConnected;
				this.everConnected = true;
				this.reconnectAttempt = 0;
				this.setStatus('open');
				// Raised before resolve() so that a request already awaiting this
				// connect cannot slip onto the socket ahead of the replay: the gate
				// has to exist by the time their continuation runs.
				if (restoring) {
					this.raiseGate();
				}
				resolve();
				if (restoring) {
					void this.replaySession();
				}
			});
			socket.addEventListener('message', (event) => {
				// A retired socket can still deliver frames while it is CLOSING, and
				// its replies belong to a connection nobody is listening to any more.
				// Without this, an id-less error from the old socket would shift
				// `outstanding` on the new one and reject an unrelated request.
				if (this.socket !== socket) {
					return;
				}
				this.handleMessage(String(event.data));
			});
			// A failed connection fires both 'error' and 'close'; rejecting an
			// already-settled promise is a no-op, so no guard is needed.
			socket.addEventListener('close', () => {
				clearTimeout(connectTimer);
				reject(new WsError('WebSocket closed'));
				this.retire(socket);
			});
		});
	}

	async request<K extends RequestType>(
		type: K,
		payload: RequestMap[K]['payload']
	): Promise<RequestMap[K]['response']> {
		return this.send(type, payload, false) as Promise<RequestMap[K]['response']>;
	}

	/**
	 * Sends a request the RequestMap does not describe. For the debug console,
	 * which builds its catalogue from the backend's own types.xml and so knows
	 * about requests this file does not — everything downstream of the frame
	 * (id correlation, timeouts, the replay gate) is the ordinary path.
	 */
	async requestRaw(type: string, payload: Record<string, unknown>): Promise<unknown> {
		return this.send(type, payload, false);
	}

	/**
	 * Puts this exact text on the socket. Nothing here validates, re-encodes or
	 * measures it: the debug console exists to be able to produce a frame the
	 * backend rejects — a bad $type, a missing field, a truncated brace — and
	 * anything that tidied the text up first would make that impossible.
	 *
	 * Correlation is therefore best-effort. A frame carrying a numeric requestId
	 * is tracked like any other request, and its reply settles this promise; one
	 * carrying none resolves as soon as the bytes are away, and whatever the
	 * backend makes of it shows up only in the traffic log.
	 */
	async sendRaw(frame: string): Promise<unknown> {
		const id = readRequestId(frame);
		if (id === null) {
			return this.dispatchUntracked(frame);
		}
		// A hand-typed id must never be handed out again, or one frame's reply
		// would settle another frame's promise.
		this.nextRequestId = Math.max(this.nextRequestId, id + 1);
		// Reusing an id is worth simulating, so the frame still goes out — but
		// `pending` holds one entry per id, and the promise this is about to
		// replace would otherwise hang until its own timeout.
		this.takePending(id)?.reject(new WsError(`requestId ${id} was reused by a later frame`));
		return this.track(id, frame, `requestId ${id}`, false);
	}

	/**
	 * Takes the next request id without sending anything, for a caller that has
	 * to show the id before the frame goes out — the debug console writes it into
	 * its editor. The counter is the client's, so two callers can never pick the
	 * same number.
	 */
	reserveRequestId(): number {
		return this.nextRequestId++;
	}

	private send(
		type: string,
		payload: Record<string, unknown>,
		privileged: boolean
	): Promise<unknown> {
		const id = this.nextRequestId++;
		return this.track(id, encodeRequest(type, id, payload), type, privileged);
	}

	/**
	 * Arms the deadline, registers the pending entry and starts the dispatch.
	 * `label` only names the caller in the timeout message: a raw frame has no
	 * type to quote.
	 */
	private track(id: number, frame: string, label: string, privileged: boolean): Promise<unknown> {
		return new Promise((resolve, reject) => {
			// Armed before connecting rather than after, so requestTimeoutMs bounds
			// the whole call. Otherwise a connection that never opens would blow
			// straight past the deadline the message below promises.
			const timer = setTimeout(() => {
				// Dropped from `pending` but left in `outstanding`: giving up on the
				// caller does not cancel the request, and the backend still owes a
				// reply that the error branch has to account for.
				this.pending.delete(id);
				reject(new WsError(`${label} timed out after ${this.requestTimeoutMs}ms`));
			}, this.requestTimeoutMs);
			this.pending.set(id, { resolve, reject, timer, sent: false });
			void this.dispatch(id, frame, privileged);
		});
	}

	/**
	 * Waits until a frame can actually be written: connected, past the replay
	 * gate, and holding a socket that is still open. Throws rather than rejecting
	 * a pending entry, because the untracked path below has none.
	 */
	private async ready(privileged: boolean): Promise<WebSocket> {
		await this.connect();
		// A reconnected socket is unauthenticated — the backend session is the
		// connection — so ordinary traffic waits behind the replay. The replay's
		// own requests are privileged, which is what keeps this from deadlocking.
		if (!privileged && this.resumeGate) {
			await this.resumeGate;
		}
		const socket = this.socket;
		// send() on a CLOSING or CLOSED socket discards the frame and throws
		// nothing, so this check is the only way the caller hears about it
		// before the timeout.
		if (!socket || socket.readyState !== WebSocket.OPEN) {
			throw new WsError('WebSocket is not open');
		}
		return socket;
	}

	private async dispatch(id: number, frame: string, privileged: boolean): Promise<void> {
		try {
			const socket = await this.ready(privileged);
			const request = this.pending.get(id);
			if (!request) {
				// Timed out while it waited for the connection or the replay.
				return;
			}
			request.sent = true;
			this.outstanding.push(id);
			socket.send(frame);
			this.dispatchFrame('out', frame);
		} catch (error) {
			this.takePending(id)?.reject(error instanceof WsError ? error : new WsError(String(error)));
		}
	}

	/**
	 * A raw frame with no requestId to track it by. The 0 pushed onto
	 * `outstanding` is a marker rather than an id: the backend answers a frame it
	 * could not deserialize with requestId 0, and handleMessage attributes such a
	 * reply to the oldest unanswered request. Without the marker that reply would
	 * reject an unrelated request; with it, the frame that caused the error
	 * absorbs it — takePending(0) finds nothing, since ids start at 1.
	 */
	private async dispatchUntracked(frame: string): Promise<undefined> {
		const socket = await this.ready(false);
		this.outstanding.push(0);
		socket.send(frame);
		this.dispatchFrame('out', frame);
		return undefined;
	}

	/**
	 * Replays the session onto a freshly reconnected socket before any ordinary
	 * request reaches it. The callback is handed a send that bypasses the gate it
	 * is itself holding.
	 */
	setResume(resume: (send: PrivilegedSend) => Promise<void>): void {
		this.resumeSession = resume;
	}

	private raiseGate(): void {
		if (this.resumeGate) {
			return;
		}
		this.resumeGate = new Promise<void>((resolve) => (this.releaseResumeGate = resolve));
	}

	private lowerGate(): void {
		this.releaseResumeGate?.();
		this.resumeGate = null;
		this.releaseResumeGate = null;
	}

	private async replaySession(): Promise<void> {
		try {
			if (this.resumeSession) {
				// The cast restores what send() gave up when it was loosened for
				// requestRaw: the caller here is typed, so its response type is known
				// even though the shared implementation no longer tracks it.
				await this.resumeSession(((type: string, payload: Record<string, unknown>) =>
					this.send(type, payload, true)) as PrivilegedSend);
			}
		} catch {
			// A failed replay leaves an open but unrestored socket, and the stores
			// show whatever the session reset left them. Swallowed rather than
			// rethrown because nothing is awaiting this, but the gate must come down
			// either way or every queued request hangs until its own timeout.
		} finally {
			this.lowerGate();
		}
	}

	onEvent<K extends EventType>(type: K, handler: (event: ServerEventOf<K>) => void): () => void {
		const handlers = this.eventHandlers.get(type) ?? new Set();
		handlers.add(handler as (event: ServerEvent) => void);
		this.eventHandlers.set(type, handlers);
		return () => handlers.delete(handler as (event: ServerEvent) => void);
	}

	/** Every server-push message, whatever its type. For diagnostics. */
	onAnyEvent(handler: (event: ServerEvent) => void): () => void {
		this.anyEventHandlers.add(handler);
		return () => this.anyEventHandlers.delete(handler);
	}

	onClose(handler: () => void): () => void {
		this.closeHandlers.add(handler);
		return () => this.closeHandlers.delete(handler);
	}

	onStatus(handler: (status: WsStatus) => void): () => void {
		this.statusHandlers.add(handler);
		return () => this.statusHandlers.delete(handler);
	}

	/**
	 * Every frame that crosses the socket, verbatim and unclassified. Purely
	 * diagnostic — the debug console's traffic log — so it sees the raw text
	 * rather than the parsed message, including one nothing could parse.
	 */
	onFrame(handler: (direction: FrameDirection, raw: string) => void): () => void {
		this.frameHandlers.add(handler);
		return () => this.frameHandlers.delete(handler);
	}

	private dispatchFrame(direction: FrameDirection, raw: string): void {
		// Snapshotted and isolated like dispatchEvent: a log that throws must not
		// take the connection down with it.
		for (const handler of [...this.frameHandlers]) {
			try {
				handler(direction, raw);
			} catch (error) {
				console.error('Frame handler threw:', error);
			}
		}
	}

	/**
	 * Closes for good: this is the logout, so it must not reconnect. Every other
	 * way a socket drops is a fault to recover from.
	 */
	close(): void {
		this.deliberatelyClosed = true;
		this.cancelReconnect();
		this.lowerGate();
		const socket = this.socket;
		if (!socket) {
			this.setStatus('closed');
			return;
		}
		// Retired before close() returns rather than on the 'close' event, which
		// is a network round trip away: readyState flips to CLOSING immediately,
		// and until the event lands connect() would keep handing back the
		// memoised promise for a socket that can no longer carry a frame.
		this.retire(socket);
		socket.close();
	}

	/**
	 * Re-arms the client after a close(). Logging in again is a fresh session
	 * rather than a recovery, so it also forgets that a connection was ever
	 * established — a first attempt that fails should surface to the login page,
	 * not start a background retry loop.
	 */
	reopen(): void {
		this.deliberatelyClosed = false;
		this.everConnected = false;
		this.cancelReconnect();
	}

	private setStatus(status: WsStatus): void {
		if (this.statusValue === status) {
			return;
		}
		this.statusValue = status;
		for (const handler of [...this.statusHandlers]) {
			handler(status);
		}
	}

	private handleMessage(raw: string): void {
		this.dispatchFrame('in', raw);
		const classified = classifyMessage(raw);
		switch (classified.kind) {
			case 'response': {
				this.clearOutstanding(classified.id);
				const pending = this.takePending(classified.id);
				pending?.resolve(classified.message as never);
				break;
			}
			case 'error': {
				// The backend echoes the failed request's id, except for a frame it
				// could not deserialize far enough to read one — it sends 0 there,
				// and 0 is never a request id. Falling back on the oldest unanswered
				// request is right for that case because the backend handles messages
				// FIFO per connection, which is why the cursor is `outstanding` and
				// not `pending`.
				const echoed = classified.message.requestId;
				const id = echoed > 0 ? echoed : this.outstanding.shift();
				if (id === undefined) {
					console.warn('Websocket error with no request to attribute it to:', classified.message);
					break;
				}
				this.clearOutstanding(id);
				// Absent from `pending` when that request already timed out: this is
				// its answer, arriving too late for anyone to receive it.
				this.takePending(id)?.reject(new WsError(classified.message.message));
				break;
			}
			case 'event':
				this.dispatchEvent(classified.message);
				break;
			case 'unknown':
				console.warn('Unrecognized websocket message:', classified.raw);
				break;
		}
	}

	private dispatchEvent(event: ServerEvent): void {
		const handlers = [...(this.eventHandlers.get(event.$type) ?? []), ...this.anyEventHandlers];
		for (const handler of handlers) {
			// Isolated so one subscriber throwing cannot rob the rest of the event.
			// Unlike a request, nobody is awaiting this to notice it went wrong.
			try {
				handler(event);
			} catch (error) {
				console.error(`Handler for ${event.$type} threw:`, error);
			}
		}
	}

	private takePending(id: number): PendingRequest | undefined {
		const pending = this.pending.get(id);
		if (pending) {
			this.pending.delete(id);
			clearTimeout(pending.timer);
		}
		return pending;
	}

	private clearOutstanding(id: number): void {
		const index = this.outstanding.indexOf(id);
		if (index !== -1) {
			this.outstanding.splice(index, 1);
		}
	}

	/**
	 * Drops a socket and everything riding on it. Reached from close() as well
	 * as from the 'close' event, and no-ops for a socket that has already been
	 * replaced so that a late close event cannot tear down its successor.
	 */
	private retire(socket: WebSocket): void {
		if (this.socket !== socket) {
			return;
		}
		this.socket = null;
		this.connectPromise = null;
		this.outstanding.length = 0;
		this.connectionGeneration++;
		for (const [id, request] of [...this.pending]) {
			if (request.sent) {
				this.takePending(id)?.reject(new WsError('WebSocket closed'));
			}
		}
		// Snapshotted like `pending` above, so a handler that subscribes or
		// unsubscribes during dispatch cannot mutate the set being iterated.
		for (const handler of [...this.closeHandlers]) {
			handler();
		}
		if (this.deliberatelyClosed || !this.everConnected) {
			this.setStatus('closed');
			return;
		}
		this.scheduleReconnect();
	}

	private scheduleReconnect(): void {
		if (this.reconnectTimer !== null) {
			return;
		}
		if (this.reconnectAttempt >= this.maxReconnectAttempts) {
			// Out of attempts: stop claiming to be recovering, so the app can treat
			// this as the session ending rather than hold position for ever.
			this.lowerGate();
			this.setStatus('closed');
			return;
		}
		this.setStatus('reconnecting');
		// Exponential with jitter — without it, every client dropped by one backend
		// restart would come back in the same instant.
		const ceiling = Math.min(
			this.reconnectMaxMs,
			this.reconnectBaseMs * 2 ** this.reconnectAttempt
		);
		const delay = ceiling * (0.5 + Math.random() / 2);
		this.reconnectAttempt++;
		this.reconnectTimer = setTimeout(() => {
			this.reconnectTimer = null;
			// Reconnecting while the machine is offline just burns an attempt, so
			// wait for the event that says it is worth trying again.
			if (typeof navigator !== 'undefined' && navigator.onLine === false) {
				// cancelReconnect() cannot reach a listener that is already armed, so
				// the close is re-checked when it fires rather than only when it is set.
				addEventListener(
					'online',
					() => {
						if (this.deliberatelyClosed) {
							return;
						}
						void this.connect().catch(() => {});
					},
					{ once: true }
				);
				return;
			}
			// The 'open' handler runs the replay; a failure routes through 'close'
			// and schedules the next attempt from retire().
			void this.connect().catch(() => {});
		}, delay);
	}

	private cancelReconnect(): void {
		if (this.reconnectTimer !== null) {
			clearTimeout(this.reconnectTimer);
			this.reconnectTimer = null;
		}
		this.reconnectAttempt = 0;
	}
}

let client: WsClient | null = null;

export function getWsClient(): WsClient {
	client ??= new WsClient({ url: resolveWsUrl() });
	return client;
}

/** The URL the app is using, or would use on its next connect. */
export function getWsUrl(): string {
	return client?.currentUrl ?? resolveWsUrl();
}

/** The URL the app would use with no override in place. */
export function getDefaultWsUrl(): string {
	return defaultWsUrl();
}

/** Whether the app is on a URL chosen here rather than the configured one. */
export function hasWsUrlOverride(): boolean {
	return readStoredWsUrl() !== null;
}

/**
 * Points the whole app at another backend, now and on every later load. The
 * caller is responsible for ending the session it is leaving — see logout() in
 * $lib/state/user.svelte, which the debug page uses for exactly that.
 */
export function setWsUrl(url: string): void {
	writeStoredWsUrl(url);
	client?.setUrl(url);
}

/** Drops the override and returns the app to the configured or dev default. */
export function clearWsUrl(): void {
	writeStoredWsUrl(null);
	client?.setUrl(defaultWsUrl());
}
