import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const DEFAULT_WS_URL = 'ws://localhost:5066/ws';
const OTHER_BACKEND = 'wss://tunnel.example/ws';

const fetchMock = vi.hoisted(() => vi.fn());
const resolveWsUrl = vi.hoisted(() => vi.fn(() => DEFAULT_WS_URL));

vi.mock('$lib/ws/ws-url', async (importOriginal) => {
	const actual = await importOriginal<typeof import('./ws-url')>();
	return {
		...actual,
		// The pure helper keeps its real implementation; only the ws-URL source
		// is stubbed.
		resolveWsUrl
	};
});

const { forgetBackendVersion, loadBackendVersion, versionState } =
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

beforeEach(() => {
	vi.stubGlobal('fetch', fetchMock);
	fetchMock.mockReset();
	// A new backing backend per case: the once-per-URL guard and any cached
	// answer left by the previous case are retired before it is asked again.
	forgetBackendVersion();
	resolveWsUrl.mockReset();
	resolveWsUrl.mockReturnValue(DEFAULT_WS_URL);
});

afterEach(() => {
	vi.unstubAllGlobals();
});

describe('loadBackendVersion', () => {
	it("asks the pointed-at backend's HTTP endpoint and keeps what it says", async () => {
		fetchMock.mockResolvedValue(serve());

		await loadBackendVersion();

		expect(resolveWsUrl).toHaveBeenCalled();
		expect(fetchMock).toHaveBeenCalledWith('http://localhost:5066/version', expect.any(Object));
		expect(versionState.backend).toEqual({ commit: COMMIT, commitTime: COMMIT_TIME });
		expect(versionState.status).toBe('loaded');
	});

	it('asks a backing URL only once, however often it is prompted', async () => {
		fetchMock.mockResolvedValue(serve());

		await loadBackendVersion();
		await loadBackendVersion();

		expect(fetchMock).toHaveBeenCalledTimes(1);
	});

	it('asks again when pointed at another backend', async () => {
		fetchMock.mockResolvedValue(serve());
		await loadBackendVersion();

		resolveWsUrl.mockReturnValue(OTHER_BACKEND);
		fetchMock.mockResolvedValue(serve({ commit: COMMIT.replace('b2', 'ff'), commitTime: null }));

		await loadBackendVersion();

		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(fetchMock).toHaveBeenLastCalledWith(
			'https://tunnel.example/version',
			expect.any(Object)
		);
		expect(versionState.backend).toEqual({
			commit: COMMIT.replace('b2', 'ff'),
			commitTime: null
		});
	});

	it('treats a backend built outside CI as local rather than unknown', async () => {
		fetchMock.mockResolvedValue(serve({}));

		await loadBackendVersion();

		expect(versionState.backend).toEqual({ commit: null, commitTime: null });
	});

	it('ignores a commitTime that is not a number', async () => {
		fetchMock.mockResolvedValue(serve({ commit: COMMIT, commitTime: 'soon' }));

		await loadBackendVersion();

		expect(versionState.backend).toEqual({ commit: COMMIT, commitTime: null });
	});

	it('leaves the backend unknown when the endpoint cannot answer', async () => {
		fetchMock.mockRejectedValue(new Error('network down'));

		await loadBackendVersion();

		expect(versionState.backend).toBeNull();
		expect(versionState.status).toBe('failed');
	});

	it('leaves the backend unknown when the endpoint answers an error', async () => {
		fetchMock.mockResolvedValue(serve({}, false));

		await loadBackendVersion();

		expect(versionState.backend).toBeNull();
		expect(versionState.status).toBe('failed');
	});

	it('discards an answer from a backend that was superseded while it was slow', async () => {
		let landFirst!: (r: ResponseLike) => void;
		fetchMock.mockReturnValueOnce(new Promise<ResponseLike>((resolve) => (landFirst = resolve)));

		const loading = loadBackendVersion();

		// A different backing backend is asked while the first is still in
		// flight; it answers immediately and wins.
		resolveWsUrl.mockReturnValue(OTHER_BACKEND);
		fetchMock.mockResolvedValueOnce(serve());
		await loadBackendVersion();

		// The old backend's answer arrives last. It must not overwrite the new.
		landFirst(serve({ commit: COMMIT.replace('b2', 'ff'), commitTime: null }));
		await loading;

		expect(versionState.backend).toEqual({ commit: COMMIT, commitTime: COMMIT_TIME });
		expect(versionState.status).toBe('loaded');
	});
});
