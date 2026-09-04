import { beforeEach, describe, expect, it, vi } from 'vitest';

const { request, connection } = vi.hoisted(() => ({
	request: vi.fn(),
	// The generation the store compares to decide whether a connection is the
	// one it already asked. Bumping it here is a socket being retired.
	connection: { generation: 0 }
}));

// One object handed back on every call, like the real singleton: the store
// reads the generation off the client it fetches, so a fresh object per call
// would be testing a contract we do not ship.
vi.mock('$lib/ws/client', () => {
	const client = {
		request,
		get generation() {
			return connection.generation;
		},
		onClose: () => () => {},
		onStatus: () => () => {},
		reopen: () => {}
	};
	return { getWsClient: () => client };
});

const { forgetBackendVersion, loadBackendVersion, versionState } =
	await import('$lib/state/version.svelte');

const COMMIT = 'b2c3d4e5f60718293a4b5c6d7e8f9012a3b4c5d6';
const COMMIT_TIME = 1_788_560_000_000;

function versionResponse(fields: object = { commit: COMMIT, commitTime: COMMIT_TIME }) {
	return { $type: 'GetVersionResponse', requestId: 1, ...fields };
}

beforeEach(() => {
	request.mockReset();
	// A new connection per case, so the once-per-connection guard left by the
	// previous case cannot swallow this one's request.
	connection.generation++;
	forgetBackendVersion();
});

describe('loadBackendVersion', () => {
	it('asks the connected backend and keeps what it says', async () => {
		request.mockResolvedValue(versionResponse());

		await loadBackendVersion();

		expect(request).toHaveBeenCalledWith('GetVersionRequest', {});
		expect(versionState.backend).toEqual({ commit: COMMIT, commitTime: COMMIT_TIME });
	});

	it('asks a connection only once, however often it is prompted', async () => {
		request.mockResolvedValue(versionResponse());

		await loadBackendVersion();
		await loadBackendVersion();

		expect(request).toHaveBeenCalledTimes(1);
	});

	it('asks again on a new connection, which may be another backend', async () => {
		request.mockResolvedValueOnce(versionResponse());
		await loadBackendVersion();

		// The socket is retired: the close handler forgets the value and the
		// generation moves on, as wireSession() and the client arrange in the app.
		forgetBackendVersion();
		connection.generation++;
		expect(versionState.backend).toBeNull();

		request.mockResolvedValueOnce(versionResponse({ commit: COMMIT.replace('b2', 'ff') }));
		await loadBackendVersion();

		expect(request).toHaveBeenCalledTimes(2);
		expect(versionState.backend).toEqual({
			commit: COMMIT.replace('b2', 'ff'),
			commitTime: null
		});
	});

	it('treats a backend built outside CI as local rather than unknown', async () => {
		request.mockResolvedValue(versionResponse({}));

		await loadBackendVersion();

		expect(versionState.backend).toEqual({ commit: null, commitTime: null });
	});

	it('leaves the backend unknown when it cannot answer', async () => {
		request.mockRejectedValue(new Error('No handler registered for this request type.'));

		await loadBackendVersion();

		expect(versionState.backend).toBeNull();
	});

	it('discards an answer from a connection that died under it', async () => {
		let land = (): void => {};
		request.mockReturnValueOnce(
			new Promise((resolve) => (land = () => resolve(versionResponse())))
		);

		const loading = loadBackendVersion();
		forgetBackendVersion();
		connection.generation++;
		land();
		await loading;

		expect(versionState.backend).toBeNull();

		// And the new connection is asked in its own right.
		request.mockResolvedValueOnce(versionResponse());
		await loadBackendVersion();
		expect(request).toHaveBeenCalledTimes(2);
		expect(versionState.backend).toEqual({ commit: COMMIT, commitTime: COMMIT_TIME });
	});
});
