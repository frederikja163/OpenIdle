import { afterEach, describe, expect, it, vi } from 'vitest';
import { WsClient, WsError, type WsClientOptions } from './client';

/**
 * Enough of the WebSocket interface for WsClient, plus levers to drive it. A
 * real socket cannot stand in here: most cases below turn on events arriving in
 * an order — or not arriving at all — that a live connection would not let a
 * test choose.
 */
class FakeSocket {
	readyState: number = WebSocket.CONNECTING;
	readonly sent: string[] = [];
	/** Frames passed to send() while not OPEN, which the real API discards. */
	readonly discarded: string[] = [];
	private readonly listeners = new Map<string, Set<(event: unknown) => void>>();

	addEventListener(type: string, handler: (event: unknown) => void): void {
		const handlers = this.listeners.get(type) ?? new Set();
		handlers.add(handler);
		this.listeners.set(type, handlers);
	}

	send(frame: string): void {
		if (this.readyState === WebSocket.OPEN) {
			this.sent.push(frame);
		} else {
			this.discarded.push(frame);
		}
	}

	/** Like the real close(): flips to CLOSING and leaves the event for later. */
	close(): void {
		if (this.readyState !== WebSocket.CLOSED) {
			this.readyState = WebSocket.CLOSING;
		}
	}

	open(): void {
		this.readyState = WebSocket.OPEN;
		this.emit('open', {});
	}

	deliver(message: object): void {
		this.emit('message', { data: JSON.stringify(message) });
	}

	/** The close handshake completing, a network round trip after close(). */
	finishClose(): void {
		this.readyState = WebSocket.CLOSED;
		this.emit('close', {});
	}

	private emit(type: string, event: unknown): void {
		for (const handler of this.listeners.get(type) ?? []) {
			handler(event);
		}
	}
}

function makeClient(options: Omit<WsClientOptions, 'url' | 'socketFactory'> = {}) {
	const sockets: FakeSocket[] = [];
	const client = new WsClient({
		url: 'ws://test.invalid/ws',
		...options,
		socketFactory: () => {
			const socket = new FakeSocket();
			sockets.push(socket);
			return socket as unknown as WebSocket;
		}
	});
	return { client, sockets };
}

/** Lets the promise handlers inside WsClient run under fake timers. */
const flush = () => vi.advanceTimersByTimeAsync(0);

/**
 * Rejections are captured rather than awaited later, so a request that is meant
 * to fail never counts as an unhandled rejection while the test drives the
 * socket around it.
 */
function capture<T>(promise: Promise<T>): Promise<T | Error> {
	return promise.catch((error: Error) => error);
}

afterEach(() => {
	vi.useRealTimers();
});

describe('WsClient', () => {
	it('resolves a request with the response carrying its id', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();

		expect(JSON.parse(sockets[0].sent[0])).toEqual({
			$type: 'LoginAsTestUserRequest',
			requestId: 1
		});
		sockets[0].deliver({ $type: 'LoginAsTestUserResponse', requestId: 1 });

		await expect(login).resolves.toEqual({ $type: 'LoginAsTestUserResponse', requestId: 1 });
	});

	it('rejects with the backend error text verbatim', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();
		sockets[0].deliver({ $type: 'ErrorResponse', requestId: 0, message: 'Already logged in.' });

		// Pinned exactly: ensureLoggedIn compares this string literally to
		// recover a hot-reloaded session, so rewording the rejection here would
		// break that with nothing else to notice.
		const error = await login;
		expect(error).toBeInstanceOf(WsError);
		expect((error as WsError).message).toBe('Already logged in.');
	});

	it('does not charge an error to a request sent after an earlier one timed out', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient({ requestTimeoutMs: 50 });

		const slow = capture(client.request('CreateProfileRequest', { name: 'Alice' }));
		await flush();
		sockets[0].open();
		await flush();

		await vi.advanceTimersByTimeAsync(50);
		expect((await slow).toString()).toMatch(/timed out after 50ms/);

		// The backend never saw the timeout and still owes a reply for id 1, so
		// the id-less error it eventually sends answers that request — not this
		// one, which merely happens to be the oldest one still pending.
		const next = capture(client.request('ListProfilesRequest', {}));
		await flush();
		sockets[0].deliver({
			$type: 'ErrorResponse',
			requestId: 0,
			message: 'Profile name already taken'
		});
		await flush();

		sockets[0].deliver({ $type: 'ListProfilesResponse', requestId: 2, profiles: [] });
		await expect(next).resolves.toEqual({
			$type: 'ListProfilesResponse',
			requestId: 2,
			profiles: []
		});
	});

	it('charges an error to the live request once an answered one is cleared', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient({ requestTimeoutMs: 50 });

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();
		sockets[0].deliver({ $type: 'LoginAsTestUserResponse', requestId: 1 });
		await expect(login).resolves.toMatchObject({ $type: 'LoginAsTestUserResponse' });

		// The counterpart to the case above: the backend has answered id 1, so
		// the cursor has to have moved off it, or this error is charged to a
		// settled request and the caller waits out the timeout for nothing.
		const create = capture(client.request('CreateProfileRequest', { name: 'Alice' }));
		await flush();
		sockets[0].deliver({
			$type: 'ErrorResponse',
			requestId: 0,
			message: 'Profile name already taken'
		});
		await vi.advanceTimersByTimeAsync(50);

		const error = await create;
		expect(error).toBeInstanceOf(WsError);
		expect((error as WsError).message).toBe('Profile name already taken');
	});

	it('charges an error carrying a request id to that request, not the oldest', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient({ requestTimeoutMs: 50 });

		const first = capture(client.request('ListProfilesRequest', {}));
		const second = capture(client.request('CreateProfileRequest', { name: 'Alice' }));
		await flush();
		sockets[0].open();
		await flush();

		// The echoed id has to beat the FIFO cursor, which still points at id 1:
		// falling back on it here would fail the wrong caller and leave the one
		// the backend actually refused waiting out its timeout.
		sockets[0].deliver({
			$type: 'ErrorResponse',
			requestId: 2,
			message: 'Profile name already taken'
		});
		await flush();

		const error = await second;
		expect(error).toBeInstanceOf(WsError);
		expect((error as WsError).message).toBe('Profile name already taken');

		sockets[0].deliver({ $type: 'ListProfilesResponse', requestId: 1, profiles: [] });
		await expect(first).resolves.toMatchObject({ $type: 'ListProfilesResponse' });
	});

	it('forgets what the backend owed when the socket drops', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient({ requestTimeoutMs: 50 });

		const abandoned = capture(client.request('CreateProfileRequest', { name: 'Alice' }));
		await flush();
		sockets[0].open();
		await flush();
		await vi.advanceTimersByTimeAsync(50);
		expect((await abandoned).toString()).toMatch(/timed out after 50ms/);

		// A tombstone outlives the caller but not the connection: the backend
		// that owed id 1 is gone, so nothing on the new socket answers it.
		sockets[0].finishClose();
		const listed = capture(client.request('ListProfilesRequest', {}));
		await flush();
		sockets[1].open();
		await flush();
		sockets[1].deliver({ $type: 'ErrorResponse', requestId: 0, message: 'Not logged in.' });
		await vi.advanceTimersByTimeAsync(50);

		const error = await listed;
		expect(error).toBeInstanceOf(WsError);
		expect((error as WsError).message).toBe('Not logged in.');
	});

	it('rejects everything in flight when the socket drops', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();
		const closed = vi.fn();
		client.onClose(closed);

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();
		sockets[0].finishClose();

		expect((await login).toString()).toMatch(/WebSocket closed/);
		expect(closed).toHaveBeenCalledTimes(1);
	});

	it('opens a fresh socket for a request made during the closing handshake', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();
		sockets[0].deliver({ $type: 'LoginAsTestUserResponse', requestId: 1 });
		await expect(login).resolves.toMatchObject({ $type: 'LoginAsTestUserResponse' });

		// close() only flips the socket to CLOSING; the event that would clear
		// the memoised connection is still a round trip away.
		client.close();
		const retry = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();

		expect(sockets).toHaveLength(2);
		sockets[1].open();
		await flush();
		expect(sockets[0].discarded).toHaveLength(0);
		expect(JSON.parse(sockets[1].sent[0])).toMatchObject({ requestId: 2 });

		// The first socket's close event lands late and must not disturb its
		// successor.
		sockets[0].finishClose();
		await flush();
		sockets[1].deliver({ $type: 'LoginAsTestUserResponse', requestId: 2 });
		await expect(retry).resolves.toMatchObject({ $type: 'LoginAsTestUserResponse' });
	});

	it('closes a socket that never finishes connecting', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient({ connectTimeoutMs: 100, requestTimeoutMs: 10_000 });

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		expect(sockets[0].readyState).toBe(WebSocket.CONNECTING);

		await vi.advanceTimersByTimeAsync(100);
		expect(sockets[0].readyState).toBe(WebSocket.CLOSING);

		sockets[0].finishClose();
		expect((await login).toString()).toMatch(/WebSocket closed/);
	});

	it('counts the connection against the request timeout', async () => {
		vi.useFakeTimers();
		const { client } = makeClient({ requestTimeoutMs: 50, connectTimeoutMs: 10_000 });

		// The socket is left in CONNECTING forever, so the only thing that can
		// settle this is a timer armed before the connection was awaited.
		const ping = capture(client.request('PingRequest', {}));
		await vi.advanceTimersByTimeAsync(50);

		expect((await ping).toString()).toMatch(/PingRequest timed out after 50ms/);
	});

	it('stops calling handlers that have unsubscribed', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();
		const onEvent = vi.fn();
		const unsubscribe = client.onAnyEvent(onEvent);

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();

		sockets[0].deliver({ $type: 'SomeFutureEvent', Value: 1 });
		expect(onEvent).toHaveBeenCalledTimes(1);

		unsubscribe();
		sockets[0].deliver({ $type: 'SomeFutureEvent', Value: 2 });
		expect(onEvent).toHaveBeenCalledTimes(1);

		sockets[0].deliver({ $type: 'LoginAsTestUserResponse', requestId: 1 });
		await expect(login).resolves.toMatchObject({ $type: 'LoginAsTestUserResponse' });
	});

	it('delivers an event only to the handlers subscribed to its type', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();
		const onProfiles = vi.fn();
		client.onEvent('ProfilesChangedEvent', onProfiles);

		capture(client.request('PingRequest', {}));
		await flush();
		sockets[0].open();
		await flush();

		sockets[0].deliver({ $type: 'SomeOtherEvent', Value: 1 });
		expect(onProfiles).not.toHaveBeenCalled();

		const profiles = [{ name: 'Thorin', profileId: 'p1' }];
		sockets[0].deliver({ $type: 'ProfilesChangedEvent', profiles });
		expect(onProfiles).toHaveBeenCalledWith({ $type: 'ProfilesChangedEvent', profiles });
	});

	it('keeps dispatching an event after one handler throws', async () => {
		vi.useFakeTimers();
		vi.spyOn(console, 'error').mockImplementation(() => {});
		const { client, sockets } = makeClient();
		const second = vi.fn();
		client.onEvent('ProfilesChangedEvent', () => {
			throw new Error('subscriber blew up');
		});
		client.onEvent('ProfilesChangedEvent', second);

		capture(client.request('PingRequest', {}));
		await flush();
		sockets[0].open();
		await flush();
		sockets[0].deliver({ $type: 'ProfilesChangedEvent', profiles: [] });

		expect(second).toHaveBeenCalledTimes(1);
	});

	it('bumps the generation when a connection is retired', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();

		const before = client.generation;
		capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();
		expect(client.generation).toBe(before);

		sockets[0].finishClose();
		expect(client.generation).toBe(before + 1);
	});

	// The bug this whole mechanism exists for: a close handler resets state, and
	// the rejected request's catch would otherwise run afterwards and overwrite it.
	it('runs close handlers before the rejections they are meant to clean up after', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();
		const order: string[] = [];
		client.onClose(() => order.push('closeHandler'));

		const login = client
			.request('LoginAsTestUserRequest', {})
			.catch(() => order.push('requestCatch'));
		await flush();
		sockets[0].open();
		await flush();

		sockets[0].finishClose();
		await login;

		expect(order).toEqual(['closeHandler', 'requestCatch']);
	});

	it('does not let a retired socket disturb its successor', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();

		capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();

		// Server-initiated close: the socket is CLOSING but its 'close' event has
		// not landed, which used to leave connect() handing back the dead memo.
		sockets[0].close();
		const next = capture(client.request('ListProfilesRequest', {}));
		await flush();
		sockets[1].open();
		await flush();

		// The old socket answers late with an id-less error. It must not be
		// charged to the new connection's request.
		sockets[0].deliver({ $type: 'ErrorResponse', requestId: 0, message: 'from the dead socket' });
		sockets[1].deliver({ $type: 'ListProfilesResponse', requestId: 2, profiles: [] });

		await expect(next).resolves.toMatchObject({ $type: 'ListProfilesResponse' });
	});

	it('lets two requests made before the socket opens ride the same one', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();

		const first = capture(client.request('LoginAsTestUserRequest', {}));
		const second = capture(client.request('ListProfilesRequest', {}));
		await flush();

		// The second request must wait for the connection the first started, not
		// mistake a socket that is merely CONNECTING for one to replace.
		expect(sockets).toHaveLength(1);
		sockets[0].open();
		await flush();

		expect(sockets[0].sent).toHaveLength(2);
		sockets[0].deliver({ $type: 'LoginAsTestUserResponse', requestId: 1 });
		sockets[0].deliver({ $type: 'ListProfilesResponse', requestId: 2, profiles: [] });
		await expect(first).resolves.toMatchObject({ $type: 'LoginAsTestUserResponse' });
		await expect(second).resolves.toMatchObject({ $type: 'ListProfilesResponse' });
	});

	it('recovers from a socket factory that throws', async () => {
		vi.useFakeTimers();
		const sockets: FakeSocket[] = [];
		let failNext = true;
		const client = new WsClient({
			url: 'ws://test.invalid/ws',
			socketFactory: () => {
				if (failNext) {
					failNext = false;
					throw new Error('mixed content');
				}
				const socket = new FakeSocket();
				sockets.push(socket);
				return socket as unknown as WebSocket;
			}
		});

		expect((await capture(client.request('PingRequest', {}))).toString()).toMatch(/mixed content/);

		// The memoised rejection must not outlive the attempt that produced it, or
		// the singleton is bricked for the life of the page.
		const retry = capture(client.request('PingRequest', {}));
		await flush();
		expect(sockets).toHaveLength(1);
		sockets[0].open();
		await flush();
		sockets[0].deliver({ $type: 'PongResponse', requestId: 2 });

		await expect(retry).resolves.toMatchObject({ $type: 'PongResponse' });
	});

	it('reconnects after an unexpected drop but not after close()', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient({ reconnectBaseMs: 100, reconnectMaxMs: 100 });

		capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();

		sockets[0].finishClose();
		expect(client.status).toBe('reconnecting');
		await vi.advanceTimersByTimeAsync(200);
		expect(sockets).toHaveLength(2);
		sockets[1].open();
		await flush();
		expect(client.status).toBe('open');

		// A deliberate close is the logout, and must stay closed.
		client.close();
		await vi.advanceTimersByTimeAsync(1000);
		expect(sockets).toHaveLength(2);
		expect(client.status).toBe('closed');
	});

	it('holds ordinary requests behind the session replay after a reconnect', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient({ reconnectBaseMs: 100, reconnectMaxMs: 100 });
		let releaseReplay = (): void => {};
		const replayed: string[] = [];
		client.setResume(async (send) => {
			await new Promise<void>((resolve) => (releaseReplay = resolve));
			await send('LoginAsTestUserRequest', {});
			replayed.push('login');
		});

		capture(client.request('PingRequest', {}));
		await flush();
		sockets[0].open();
		await flush();
		sockets[0].finishClose();

		await vi.advanceTimersByTimeAsync(200);
		sockets[1].open();
		await flush();

		// The replay is still blocked, so an ordinary request must not be sent yet.
		capture(client.request('ListProfilesRequest', {}));
		await flush();
		expect(sockets[1].sent).toHaveLength(0);

		releaseReplay();
		await flush();
		// The replay's own request is privileged, so it goes out while the gate it
		// is holding still blocks the queued one.
		expect(sockets[1].sent.map((frame) => JSON.parse(frame).$type)).toEqual([
			'LoginAsTestUserRequest'
		]);

		sockets[1].deliver({
			$type: 'LoginAsTestUserResponse',
			requestId: JSON.parse(sockets[1].sent[0]).requestId
		});
		await flush();

		expect(replayed).toEqual(['login']);
		expect(sockets[1].sent.map((frame) => JSON.parse(frame).$type)).toEqual([
			'LoginAsTestUserRequest',
			'ListProfilesRequest'
		]);
	});
});
