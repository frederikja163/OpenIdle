import { dev } from '$app/environment';
import { env } from '$env/dynamic/public';
import {
	classifyMessage,
	encodeRequest,
	type RequestMap,
	type RequestType,
	type ServerEvent
} from './protocol';

const DEFAULT_WS_URL = 'ws://localhost:5066/ws';
const DEFAULT_REQUEST_TIMEOUT_MS = 10_000;
const DEFAULT_CONNECT_TIMEOUT_MS = 5_000;

export class WsError extends Error {}

export interface WsClientOptions {
	url: string;
	requestTimeoutMs?: number;
	connectTimeoutMs?: number;
	socketFactory?: (url: string) => WebSocket;
}

interface PendingRequest {
	resolve: (response: never) => void;
	reject: (error: WsError) => void;
	timer: ReturnType<typeof setTimeout>;
}

export class WsClient {
	private readonly url: string;
	private readonly requestTimeoutMs: number;
	private readonly connectTimeoutMs: number;
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
	private readonly eventHandlers = new Set<(event: ServerEvent) => void>();
	private readonly closeHandlers = new Set<() => void>();

	constructor(options: WsClientOptions) {
		this.url = options.url;
		this.requestTimeoutMs = options.requestTimeoutMs ?? DEFAULT_REQUEST_TIMEOUT_MS;
		this.connectTimeoutMs = options.connectTimeoutMs ?? DEFAULT_CONNECT_TIMEOUT_MS;
		this.socketFactory = options.socketFactory ?? ((url) => new WebSocket(url));
	}

	connect(): Promise<void> {
		if (this.socket?.readyState === WebSocket.OPEN) {
			return Promise.resolve();
		}
		this.connectPromise ??= new Promise((resolve, reject) => {
			const socket = this.socketFactory(this.url);
			this.socket = socket;
			// A socket whose TCP handshake completes but whose upgrade never does
			// stays in CONNECTING until the browser's own transport timeout, tens
			// of seconds away. Closing it routes the failure through the 'close'
			// listener like any other, so nothing else needs to know about it.
			const connectTimer = setTimeout(() => socket.close(), this.connectTimeoutMs);
			socket.addEventListener('open', () => {
				clearTimeout(connectTimer);
				resolve();
			});
			socket.addEventListener('message', (event) => this.handleMessage(String(event.data)));
			// A failed connection fires both 'error' and 'close'; rejecting an
			// already-settled promise is a no-op, so no guard is needed.
			socket.addEventListener('close', () => {
				clearTimeout(connectTimer);
				reject(new WsError('WebSocket closed'));
				this.retire(socket);
			});
		});
		return this.connectPromise;
	}

	async request<K extends RequestType>(
		type: K,
		payload: RequestMap[K]['payload']
	): Promise<RequestMap[K]['response']> {
		const id = this.nextRequestId++;
		const frame = encodeRequest(type, id, payload);
		return new Promise((resolve, reject) => {
			// Armed before connecting rather than after, so requestTimeoutMs bounds
			// the whole call. Otherwise a connection that never opens would blow
			// straight past the deadline the message below promises.
			const timer = setTimeout(() => {
				// Dropped from `pending` but left in `outstanding`: giving up on the
				// caller does not cancel the request, and the backend still owes a
				// reply that the error branch has to account for.
				this.pending.delete(id);
				reject(new WsError(`${type} timed out after ${this.requestTimeoutMs}ms`));
			}, this.requestTimeoutMs);
			this.pending.set(id, { resolve, reject, timer });
			this.connect().then(
				() => {
					const socket = this.socket;
					// send() on a CLOSING or CLOSED socket discards the frame and
					// throws nothing, so this check is the only way the caller hears
					// about it before the timeout.
					if (!socket || socket.readyState !== WebSocket.OPEN) {
						this.takePending(id)?.reject(new WsError('WebSocket is not open'));
						return;
					}
					this.outstanding.push(id);
					socket.send(frame);
				},
				(error: unknown) => {
					this.takePending(id)?.reject(
						error instanceof WsError ? error : new WsError(String(error))
					);
				}
			);
		});
	}

	onEvent(handler: (event: ServerEvent) => void): () => void {
		this.eventHandlers.add(handler);
		return () => this.eventHandlers.delete(handler);
	}

	onClose(handler: () => void): () => void {
		this.closeHandlers.add(handler);
		return () => this.closeHandlers.delete(handler);
	}

	close(): void {
		const socket = this.socket;
		if (!socket) {
			return;
		}
		// Retired before close() returns rather than on the 'close' event, which
		// is a network round trip away: readyState flips to CLOSING immediately,
		// and until the event lands connect() would keep handing back the
		// memoised promise for a socket that can no longer carry a frame.
		this.retire(socket);
		socket.close();
	}

	private handleMessage(raw: string): void {
		const classified = classifyMessage(raw);
		switch (classified.kind) {
			case 'response': {
				this.clearOutstanding(classified.id);
				const pending = this.takePending(classified.id);
				pending?.resolve(classified.message as never);
				break;
			}
			case 'error': {
				// ErrorResponse.Id is always null on the backend, so it cannot be
				// matched by id. The backend handles messages FIFO per connection,
				// so the error answers the oldest request it has not replied to —
				// which is why the cursor is `outstanding` and not `pending`.
				const id = this.outstanding.shift();
				if (id === undefined) {
					console.warn('Websocket error with no request to attribute it to:', classified.message);
					break;
				}
				// Absent from `pending` when that request already timed out: this is
				// its answer, arriving too late for anyone to receive it.
				this.takePending(id)?.reject(new WsError(classified.message.Message));
				break;
			}
			case 'event': {
				for (const handler of this.eventHandlers) {
					handler(classified.message);
				}
				break;
			}
			case 'unknown':
				console.warn('Unrecognized websocket message:', classified.raw);
				break;
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
		for (const id of [...this.pending.keys()]) {
			this.takePending(id)?.reject(new WsError('WebSocket closed'));
		}
		for (const handler of this.closeHandlers) {
			handler();
		}
	}
}

let client: WsClient | null = null;

export function getWsClient(): WsClient {
	client ??= new WsClient({ url: resolveWsUrl() });
	return client;
}

/**
 * The development default is only safe for a local backend. In any deployed
 * build a missing PUBLIC_WS_URL is a configuration error worth failing fast on
 * rather than silently pointing every client at localhost.
 */
function resolveWsUrl(): string {
	const configured = env.PUBLIC_WS_URL;
	if (configured) {
		return configured;
	}
	if (dev) {
		return DEFAULT_WS_URL;
	}
	console.warn(
		'PUBLIC_WS_URL is not set; refusing to fall back to the development default outside development.'
	);
	throw new Error('PUBLIC_WS_URL must be configured outside development');
}
