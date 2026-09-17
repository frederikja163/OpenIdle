import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const fetchMock = vi.hoisted(() => vi.fn());
const getApiUrl = vi.hoisted(() => vi.fn(() => 'http://localhost:5066'));

// The catalogue must come from the backend the app is pointed at, and nothing
// else from the client: asking for a contract must not pull a socket open.
vi.mock('$lib/ws/client', () => ({ getApiUrl }));

const { ensureProtocolSchema, loadProtocolSchema, schemaState } = await import('./schema.svelte');

/** The smallest contract a form can be built from. */
function contract(requestName = 'StopActivityRequest') {
	return {
		Enums: [],
		Dtos: [],
		Requests: [{ Name: requestName, Properties: [], Responses: [{ Name: null, Properties: [] }] }],
		Events: []
	};
}

interface ResponseLike {
	ok: boolean;
	status: number;
	json: () => Promise<unknown>;
}

function serve(body: unknown = contract(), status = 200): ResponseLike {
	return { ok: status < 400, status, json: vi.fn().mockResolvedValue(body) };
}

// The module keeps the last-asked URL across tests, so each test points the
// client somewhere of its own rather than inheriting the previous test's state.
let backends = 0;
function pointAt(apiUrl = `http://backend-${++backends}.example`): string {
	getApiUrl.mockReturnValue(apiUrl);
	return `${apiUrl}/schema`;
}

/** The AbortSignal the loader handed to the n-th fetch. */
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

describe('ensureProtocolSchema', () => {
	it('asks the pointed-at backend for its contract and maps what it says', async () => {
		const url = pointAt();
		fetchMock.mockResolvedValue(serve());

		await ensureProtocolSchema();

		expect(fetchMock).toHaveBeenCalledWith(url, expect.any(Object));
		expect(schemaState.status).toBe('loaded');
		expect(schemaState.protocol?.requests).toEqual([
			{
				typeName: 'StopActivityRequest',
				properties: [],
				response: { typeName: 'StopActivityRequestResponse', properties: [] }
			}
		]);
	});

	it('asks a backend once, however many times the console opens', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve());

		await ensureProtocolSchema();
		await ensureProtocolSchema();

		expect(fetchMock).toHaveBeenCalledTimes(1);
	});

	it('asks again when the app is pointed at another backend', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve());
		await ensureProtocolSchema();

		const moved = pointAt('https://tunnel.example');
		fetchMock.mockResolvedValue(serve(contract('GetItemsRequest')));
		await ensureProtocolSchema();

		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(fetchMock).toHaveBeenLastCalledWith(moved, expect.any(Object));
		expect(schemaState.protocol?.requests[0].typeName).toBe('GetItemsRequest');
	});

	it('retries a backend that failed to answer', async () => {
		pointAt();
		fetchMock.mockRejectedValueOnce(new Error('network down'));
		await ensureProtocolSchema();
		expect(schemaState.status).toBe('failed');

		fetchMock.mockResolvedValue(serve());
		await ensureProtocolSchema();

		expect(fetchMock).toHaveBeenCalledTimes(2);
		expect(schemaState.status).toBe('loaded');
	});
});

describe('loadProtocolSchema', () => {
	it('drops the old catalogue while a different backend is asked', async () => {
		pointAt();
		fetchMock.mockResolvedValueOnce(serve());
		await loadProtocolSchema();

		pointAt();
		fetchMock.mockReturnValueOnce(new Promise(() => {}));
		void loadProtocolSchema();

		// Unlike the version footer, which keeps showing a build it knows: a
		// catalogue from the previous backend would offer requests this one may not
		// have.
		expect(schemaState.status).toBe('loading');
		expect(schemaState.protocol).toBeNull();
	});

	it('discards an answer from a backend that was superseded while it was slow', async () => {
		pointAt();
		let answerSlowly = (): void => {};
		fetchMock.mockReturnValueOnce(
			new Promise<ResponseLike>((resolve) => (answerSlowly = () => resolve(serve())))
		);
		const slow = loadProtocolSchema();

		pointAt();
		fetchMock.mockResolvedValueOnce(serve(contract('GetItemsRequest')));
		await loadProtocolSchema();

		answerSlowly();
		await slow;

		expect(signalOf(0).aborted).toBe(true);
		expect(schemaState.protocol?.requests[0].typeName).toBe('GetItemsRequest');
		expect(schemaState.status).toBe('loaded');
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

		const asking = loadProtocolSchema();
		await vi.advanceTimersByTimeAsync(4_999);
		expect(schemaState.status).toBe('loading');

		await vi.advanceTimersByTimeAsync(1);
		await asking;

		expect(signalOf(0).aborted).toBe(true);
		expect(schemaState.status).toBe('failed');
	});

	it('says which address failed and how', async () => {
		const url = pointAt();
		// What a backend built before /schema existed answers with. 'failed' alone
		// would not tell whoever is on the page that the backend is simply too old.
		fetchMock.mockResolvedValue(serve({}, 404));

		await loadProtocolSchema();

		expect(schemaState.protocol).toBeNull();
		expect(schemaState.status).toBe('failed');
		expect(schemaState.error).toContain(url);
		expect(schemaState.error).toContain('404');
	});

	it('reports a contract it cannot read in the words of the mapping', async () => {
		pointAt();
		fetchMock.mockResolvedValue(serve({ Requests: [{ Name: 'BadRequest', Responses: [] }] }));

		await loadProtocolSchema();

		expect(schemaState.status).toBe('failed');
		expect(schemaState.error).toBe('BadRequest must declare exactly one response.');
	});

	it('clears a previous failure once a backend answers', async () => {
		pointAt();
		fetchMock.mockRejectedValueOnce(new Error('network down'));
		await loadProtocolSchema();
		expect(schemaState.error).not.toBeNull();

		fetchMock.mockResolvedValue(serve());
		await loadProtocolSchema();

		expect(schemaState.error).toBeNull();
		expect(schemaState.status).toBe('loaded');
	});
});
