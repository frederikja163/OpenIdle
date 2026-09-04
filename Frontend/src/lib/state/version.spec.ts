import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const DEFAULT_WS_URL = 'ws://localhost:5066/ws';

const fetchMock = vi.hoisted(() => vi.fn());
const resolveWsUrl = vi.hoisted(() => vi.fn(() => DEFAULT_WS_URL));

vi.mock('$lib/ws/ws-url', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/ws/ws-url')>();
	return {
		...actual,
		// The pure helper keeps its real implementation; only the ws-URL source
		// is stubbed.
		resolveWsUrl
	};
});

const { loadBackendVersion, versionState } = await import('$lib/state/version.svelte');

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

	it('re-asks whenever prompted, so a moved backend is picked up', async () => {
		fetchMock.mockResolvedValue(serve());
		await loadBackendVersion();

		resolveWsUrl.mockReturnValue('wss://tunnel.example/ws');
		await loadBackendVersion();

		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(fetchMock).toHaveBeenLastCalledWith(
			'https://tunnel.example/version',
			expect.any(Object)
		);
	});

	it('treats a backend built outside CI as local rather than unknown', async () => {
		fetchMock.mockResolvedValue(serve({}));

		await loadBackendVersion();

		expect(versionState.backend).toEqual({ commit: null, commitTime: null });
		expect(versionState.status).toBe('loaded');
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

		fetchMock.mockResolvedValue(serve({}, false));
		await loadBackendVersion();
		expect(versionState.backend).toBeNull();
		expect(versionState.status).toBe('failed');
	});
});
