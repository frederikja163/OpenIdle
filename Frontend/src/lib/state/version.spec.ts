import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const fetchMock = vi.hoisted(() => vi.fn());
const getWsUrl = vi.hoisted(() => vi.fn(() => 'ws://localhost:5066/ws'));

// The store must read the URL the client is actually pointed at, and nothing
// else from the client: the footer never opens a socket.
vi.mock('$lib/ws/client', () => ({ getWsUrl }));

const { ensureBackendVersion, loadBackendVersion, versionState } =
	await import('$lib/state/version.svelte');

const COMMIT = 'b2c3d4e5f60718293a4b5c6d7e8f9012a3b4c5d6';
const COMMIT_TIME = 1_788_560_000_000;

interface ResponseLike {
	ok: boolean;
	json: () => Promise<unknown>;
}

function serve(
	body: unknown = { commit: COMMIT, commitTime: COMMIT_TIME },
	ok = true
): ResponseLike {
	return { ok, json: vi.fn().mockResolvedValue(body) };
}

// The module keeps the last-asked URL across tests, so each test points the
// client somewhere of its own rather than inheriting the previous test's state.
let backends = 0;
function pointAt(wsUrl = `ws://backend-${++backends}.example/ws`): string {
	getWsUrl.mockReturnValue(wsUrl);
	return wsUrl.replace(/^ws/, 'http').replace(/\/ws$/, '/version');
}

/** The AbortSignal the store handed to the n-th fetch. */
function signalOf(call: number): AbortSignal {
	return (fetchMock.mock.calls[call][1] as { signal: AbortSignal }).signal;
}

beforeEach(() => {
	vi.stubGlobal('fetch', fetchMock);
	fetchMock.mockReset();
});

afterEach(() => {
	vi.unstubAllGlobals();
	vi.useRealTimers();
});

describe('ensureBackendVersion', () => {
	it("asks the pointed-at backend's HTTP endpoint and keeps what it says", async () => {
		const url = pointAt();
		fetchMock.mockResolvedValue(serve());

		await ensureBackendVersion();

		expect(fetchMock).toHaveBeenCalledWith(url, expect.any(Object));
		expect(versionState.backend).toEqual({ commit: COMMIT, commitTime: COMMIT_TIME });
		expect(versionState.status).toBe('loaded');
	});

	it('asks a backend once, however many footers mount', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve());

		await ensureBackendVersion();
		await ensureBackendVersion();

		expect(fetchMock).toHaveBeenCalledTimes(1);
	});

	it('asks again when the client is pointed at another backend', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve());
		await ensureBackendVersion();

		const moved = pointAt('wss://tunnel.example/ws');
		await ensureBackendVersion();

		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(fetchMock).toHaveBeenLastCalledWith(moved, expect.any(Object));
	});

	it('retries a backend that failed to answer', async () => {
		pointAt();
		fetchMock.mockRejectedValueOnce(new Error('network down'));
		await ensureBackendVersion();
		expect(versionState.status).toBe('failed');

		fetchMock.mockResolvedValue(serve());
		await ensureBackendVersion();

		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(versionState.status).toBe('loaded');
	});
});

describe('loadBackendVersion', () => {
	it('asks afresh every time, so a redeployed backend is picked up on reconnect', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve());

		await loadBackendVersion();
		await loadBackendVersion();

		expect(fetchMock).toHaveBeenCalledTimes(2);
	});

	it('keeps the known value visible while the same backend is asked again', async () => {
		pointAt();
		fetchMock.mockResolvedValueOnce(serve());
		await loadBackendVersion();

		fetchMock.mockReturnValueOnce(new Promise(() => {}));
		void loadBackendVersion();

		expect(versionState.status).toBe('loading');
		expect(versionState.backend).toEqual({ commit: COMMIT, commitTime: COMMIT_TIME });
	});

	it('discards an answer from a backend that was superseded while it was slow', async () => {
		pointAt();
		let answerSlowly = (): void => {};
		fetchMock.mockReturnValueOnce(
			new Promise<ResponseLike>((resolve) => (answerSlowly = () => resolve(serve())))
		);
		const slow = loadBackendVersion();

		pointAt();
		fetchMock.mockResolvedValueOnce(serve({ commit: COMMIT.replace('b2', 'ff') }));
		await loadBackendVersion();

		answerSlowly();
		await slow;

		expect(signalOf(0).aborted).toBe(true);
		expect(versionState.backend).toEqual({ commit: COMMIT.replace('b2', 'ff'), commitTime: null });
		expect(versionState.status).toBe('loaded');
	});

	it('gives up on a backend that never answers', async () => {
		vi.useFakeTimers();
		pointAt();
		fetchMock.mockImplementation(
			(_url: string, init: { signal: AbortSignal }) =>
				new Promise((_resolve, reject) =>
					init.signal.addEventListener('abort', () => reject(new Error('aborted')))
				)
		);

		const asking = loadBackendVersion();
		await vi.advanceTimersByTimeAsync(4_999);
		expect(versionState.status).toBe('loading');

		await vi.advanceTimersByTimeAsync(1);
		await asking;

		expect(signalOf(0).aborted).toBe(true);
		expect(versionState.status).toBe('failed');
	});

	it('treats a backend built outside CI as local rather than unknown', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve({}));

		await loadBackendVersion();

		expect(versionState.backend).toEqual({ commit: null, commitTime: null });
		expect(versionState.status).toBe('loaded');
	});

	it('ignores a commitTime that is not a number', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve({ commit: COMMIT, commitTime: 'soon' }));

		await loadBackendVersion();

		expect(versionState.backend).toEqual({ commit: COMMIT, commitTime: null });
	});

	it('leaves the backend unknown when the endpoint cannot answer', async () => {
		pointAt();
		fetchMock.mockRejectedValue(new Error('network down'));
		await loadBackendVersion();
		expect(versionState.backend).toBeNull();
		expect(versionState.status).toBe('failed');

		fetchMock.mockResolvedValue(serve({}, false));
		await loadBackendVersion();
		expect(versionState.backend).toBeNull();
		expect(versionState.status).toBe('failed');
	});
});
