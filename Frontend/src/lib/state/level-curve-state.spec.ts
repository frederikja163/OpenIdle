import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const fetchMock = vi.hoisted(() => vi.fn());
const getApiUrl = vi.hoisted(() => vi.fn(() => 'http://localhost:5066'));

// The store must read the API base the client is actually pointed at, and
// nothing else from the client: the curve never opens a socket.
vi.mock('$lib/ws/client', () => ({ getApiUrl }));

const { ensureLevelCurve, loadLevelCurve, levelCurveState } =
	await import('$lib/state/level-curve.svelte');

const LEVELS = [0, 895, 1906, 3049, 4340];

interface ResponseLike {
	ok: boolean;
	json: () => Promise<unknown>;
}

function serve(body: unknown = LEVELS, ok = true): ResponseLike {
	return { ok, json: vi.fn().mockResolvedValue(body) };
}

// The module keeps the last-asked URL across tests, so each test points the
// client somewhere of its own rather than inheriting the previous test's state.
let backends = 0;
function pointAt(apiUrl = `http://backend-${++backends}.example`): string {
	getApiUrl.mockReturnValue(apiUrl);
	return `${apiUrl}/levels`;
}

beforeEach(() => {
	vi.stubGlobal('fetch', fetchMock);
	fetchMock.mockReset();
});

afterEach(() => {
	vi.unstubAllGlobals();
});

describe('ensureLevelCurve', () => {
	it("asks the pointed-at backend's HTTP endpoint and keeps what it says", async () => {
		const url = pointAt();
		fetchMock.mockResolvedValue(serve());

		await ensureLevelCurve();

		expect(fetchMock).toHaveBeenCalledWith(url, expect.any(Object));
		expect(levelCurveState.levels).toEqual(LEVELS);
		expect(levelCurveState.status).toBe('loaded');
	});

	it('asks a backend once, however many callers ask', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve());

		await ensureLevelCurve();
		await ensureLevelCurve();

		expect(fetchMock).toHaveBeenCalledTimes(1);
	});

	it('asks again when the client is pointed at another backend', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve());
		await ensureLevelCurve();

		const moved = pointAt('https://tunnel.example');
		await ensureLevelCurve();

		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(fetchMock).toHaveBeenLastCalledWith(moved, expect.any(Object));
	});

	it('retries a backend that failed to answer', async () => {
		pointAt();
		fetchMock.mockRejectedValueOnce(new Error('network down'));
		await ensureLevelCurve();
		expect(levelCurveState.status).toBe('failed');

		fetchMock.mockResolvedValueOnce(serve());
		await ensureLevelCurve();
		expect(levelCurveState.status).toBe('loaded');
	});
});

describe('loadLevelCurve', () => {
	it('discards a body that is not a table of levels', async () => {
		pointAt();
		fetchMock.mockResolvedValueOnce(serve({ nope: true }));
		await loadLevelCurve();
		expect(levelCurveState.levels).toBeNull();
		expect(levelCurveState.status).toBe('failed');

		fetchMock.mockResolvedValueOnce(serve([0, 'x', 2]));
		await loadLevelCurve();
		expect(levelCurveState.status).toBe('failed');
	});

	it('rejects a non-200 answer', async () => {
		pointAt();
		fetchMock.mockResolvedValueOnce(serve(LEVELS, false));

		await loadLevelCurve();

		expect(levelCurveState.levels).toBeNull();
		expect(levelCurveState.status).toBe('failed');
	});

	it('forgets the previous table when the backend changes', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve());
		await ensureLevelCurve();
		expect(levelCurveState.levels).toEqual(LEVELS);

		pointAt('https://tunnel.example');
		let release = (): void => {};
		fetchMock.mockReturnValueOnce(new Promise((resolve) => (release = () => resolve(serve()))));
		const loading = loadLevelCurve();

		// The old backend's table must not stand in for the new backend's.
		expect(levelCurveState.levels).toBeNull();

		release();
		await loading;
		expect(levelCurveState.levels).toEqual(LEVELS);
	});
});
