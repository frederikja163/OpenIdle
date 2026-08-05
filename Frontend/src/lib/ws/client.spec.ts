import { afterEach, describe, expect, it, vi } from 'vitest';
import { WsClient, WsError } from './client';

/**
 * Enough of the WebSocket interface for WsClient, plus levers to drive it. A
 * real socket cannot stand in here: most cases below turn on events arriving in
 * an order — or not arriving at all — that a live connection would not let a
 * test choose.
 */
class FakeSocket {
	readyState: number = WebSocket.CONNECTING;
	readonly sent: string[] = [];
	private readonly listeners = new Map<string, Set<(event: unknown) => void>>();

	addEventListener(type: string, handler: (event: unknown) => void): void {
		const handlers = this.listeners.get(type) ?? new Set();
		handlers.add(handler);
		this.listeners.set(type, handlers);
	}

	send(frame: string): void {
		if (this.readyState === WebSocket.OPEN) {
			this.sent.push(frame);
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

function makeClient(options: { requestTimeoutMs?: number } = {}) {
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

		expect(JSON.parse(sockets[0].sent[0])).toEqual({ $type: 'LoginAsTestUserRequest', Id: 1 });
		sockets[0].deliver({ $type: 'LoginAsTestUserResponse', Id: 1 });

		await expect(login).resolves.toEqual({ $type: 'LoginAsTestUserResponse', Id: 1 });
	});

	it('rejects with the backend error text verbatim', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();
		sockets[0].deliver({ $type: 'ErrorResponse', Id: null, Message: 'Already logged in.' });

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

		const slow = capture(client.request('CreateProfileRequest', { Name: 'Alice' }));
		await flush();
		sockets[0].open();
		await flush();

		await vi.advanceTimersByTimeAsync(50);
		expect((await slow).toString()).toMatch(/timed out after 50ms/);

		// The backend never saw the timeout and still owes a reply for id 1, so
		// the Id-less error it eventually sends answers that request — not this
		// one, which merely happens to be the oldest one still pending.
		const next = capture(client.request('ListProfilesRequest', {}));
		await flush();
		sockets[0].deliver({ $type: 'ErrorResponse', Id: null, Message: 'Profile name already taken' });
		await flush();

		sockets[0].deliver({ $type: 'ListProfilesResponse', Id: 2, Profiles: [] });
		await expect(next).resolves.toEqual({ $type: 'ListProfilesResponse', Id: 2, Profiles: [] });
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

	it('stops calling handlers that have unsubscribed', async () => {
		vi.useFakeTimers();
		const { client, sockets } = makeClient();
		const onEvent = vi.fn();
		const unsubscribe = client.onEvent(onEvent);

		const login = capture(client.request('LoginAsTestUserRequest', {}));
		await flush();
		sockets[0].open();
		await flush();

		sockets[0].deliver({ $type: 'SomeFutureEvent', Value: 1 });
		expect(onEvent).toHaveBeenCalledTimes(1);

		unsubscribe();
		sockets[0].deliver({ $type: 'SomeFutureEvent', Value: 2 });
		expect(onEvent).toHaveBeenCalledTimes(1);

		sockets[0].deliver({ $type: 'LoginAsTestUserResponse', Id: 1 });
		await expect(login).resolves.toMatchObject({ $type: 'LoginAsTestUserResponse' });
	});
});
