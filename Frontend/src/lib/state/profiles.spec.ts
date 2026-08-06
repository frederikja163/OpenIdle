import { beforeEach, describe, expect, it, vi } from 'vitest';

const { request, connection } = vi.hoisted(() => ({
	request: vi.fn(),
	// The generation the store reads to decide whether a result still belongs to
	// the live connection. Bumping it here is a socket dropping.
	connection: { generation: 0 }
}));

// One object handed back on every call, like the real singleton: sessionRun
// compares the generation it captured against the one on the client it fetches
// later, so a fresh object per call would be testing a contract we do not ship.
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

const { createProfile, loadProfiles, profilesState, selectProfile, validateProfileName } =
	await import('$lib/state/profiles.svelte');
// wireSession() registers the reset against the socket in the real app; this
// project never calls it, so the reset is driven directly here instead — which
// is what makes a connection dropping testable at all.
const { forgetSessionIntent, resetSessionState, sessionIntent } =
	await import('$lib/state/session.svelte');

const THORIN = { Name: 'Thorin', ProfileId: '11111111-1111-1111-1111-111111111111' };
const BALIN = { Name: 'Balin', ProfileId: '22222222-2222-2222-2222-222222222222' };

function listResponse(profiles: (typeof THORIN)[]) {
	return { $type: 'ListProfilesResponse', Id: 1, Profiles: profiles };
}

beforeEach(() => {
	request.mockReset();
	connection.generation = 0;
	// One call each, no field lists: a field added to the store or to the intent
	// and forgotten here can no longer leak between cases.
	resetSessionState();
	forgetSessionIntent();
});

describe('loadProfiles', () => {
	it('asks for ListProfilesRequest and keeps the profiles it gets back', async () => {
		request.mockResolvedValue(listResponse([THORIN]));

		await loadProfiles();

		expect(request).toHaveBeenCalledWith('ListProfilesRequest', {});
		expect(profilesState.profiles).toEqual([THORIN]);
		expect(profilesState.status).toBe('loaded');
		expect(profilesState.error).toBeNull();
	});

	it('surfaces the backend message when the request fails', async () => {
		request.mockRejectedValue(new Error("Value cannot be null. (Parameter 'User')"));

		await loadProfiles();

		expect(profilesState.status).toBe('error');
		expect(profilesState.error).toBe("Value cannot be null. (Parameter 'User')");
		expect(profilesState.profiles).toEqual([]);
	});
});

describe('validateProfileName', () => {
	it('accepts a name the backend would accept', () => {
		expect(validateProfileName('Thorin1')).toBeNull();
		// The boundary itself, against the rejection of 31 below.
		expect(validateProfileName('a'.repeat(30))).toBeNull();
	});

	it.each([
		['', 'Enter a profile name.'],
		['a'.repeat(31), 'Profile name must be at most 30 characters.'],
		['Thorin!', 'Profile name must be letters and digits only.'],
		['Thorin Oakenshield', 'Profile name must be letters and digits only.'],
		['Thorin_', 'Profile name must be letters and digits only.']
	])('rejects %j', (name, message) => {
		expect(validateProfileName(name)).toBe(message);
	});
});

describe('createProfile', () => {
	function respondByType(profilesAfterCreate: (typeof THORIN)[]) {
		request.mockImplementation((type: string) =>
			type === 'CreateProfileRequest'
				? Promise.resolve({ $type: 'CreateProfileResponse', Id: 1 })
				: Promise.resolve(listResponse(profilesAfterCreate))
		);
	}

	it('sends the name and then refetches the list to learn the new profile', async () => {
		respondByType([THORIN]);

		await expect(createProfile('Thorin')).resolves.toBe(true);

		expect(request).toHaveBeenNthCalledWith(1, 'CreateProfileRequest', { Name: 'Thorin' });
		expect(request).toHaveBeenNthCalledWith(2, 'ListProfilesRequest', {});
		expect(profilesState.profiles).toEqual([THORIN]);
		expect(profilesState.creating).toBe(false);
		expect(profilesState.createError).toBeNull();
	});

	it('drops surrounding whitespace rather than failing the alphanumeric check', async () => {
		respondByType([THORIN]);

		await createProfile('  Thorin  ');

		expect(request).toHaveBeenNthCalledWith(1, 'CreateProfileRequest', { Name: 'Thorin' });
	});

	it('refuses an invalid name without spending a round trip', async () => {
		await expect(createProfile('bad name!')).resolves.toBe(false);

		expect(request).not.toHaveBeenCalled();
		expect(profilesState.createError).toBe('Profile name must be letters and digits only.');
	});

	it('surfaces the backend message and does not refetch when the create fails', async () => {
		request.mockRejectedValue(new Error('UNIQUE constraint failed: Profiles.Name'));

		await expect(createProfile('Thorin')).resolves.toBe(false);

		expect(request).toHaveBeenCalledTimes(1);
		expect(profilesState.createError).toBe('UNIQUE constraint failed: Profiles.Name');
		expect(profilesState.creating).toBe(false);
	});

	it('leaves the loaded list untouched when the create fails', async () => {
		profilesState.status = 'loaded';
		profilesState.profiles = [BALIN];
		request.mockRejectedValue(new Error('UNIQUE constraint failed: Profiles.Name'));

		await createProfile('Balin');

		expect(profilesState.status).toBe('loaded');
		expect(profilesState.profiles).toEqual([BALIN]);
		expect(profilesState.error).toBeNull();
	});

	it('ignores a second submit while one is still in flight', async () => {
		let release = (): void => {};
		request.mockReturnValueOnce(new Promise((resolve) => (release = () => resolve(undefined))));

		const first = createProfile('Thorin');
		await expect(createProfile('Balin')).resolves.toBe(false);

		expect(request).toHaveBeenCalledTimes(1);
		release();
		await first;
	});
});

describe('selectProfile', () => {
	it('points the socket at the profile and records the selection', async () => {
		request.mockResolvedValue({ $type: 'SelectProfileResponse', Id: 1 });

		await expect(selectProfile(THORIN.ProfileId)).resolves.toBe(true);

		expect(request).toHaveBeenCalledWith('SelectProfileRequest', { ProfileId: THORIN.ProfileId });
		expect(profilesState.selectedProfileId).toBe(THORIN.ProfileId);
		expect(profilesState.selectingProfileId).toBeNull();
		expect(profilesState.selectError).toBeNull();
	});

	it('keeps the previous selection when the backend refuses', async () => {
		profilesState.selectedProfileId = BALIN.ProfileId;
		request.mockRejectedValue(new Error('Profile does not belong to user.'));

		await expect(selectProfile(THORIN.ProfileId)).resolves.toBe(false);

		expect(profilesState.selectError).toBe('Profile does not belong to user.');
		expect(profilesState.selectedProfileId).toBe(BALIN.ProfileId);
	});

	it('marks only the profile being selected as in flight', async () => {
		let release = (): void => {};
		request.mockReturnValueOnce(new Promise((resolve) => (release = () => resolve(undefined))));

		const pending = selectProfile(THORIN.ProfileId);

		expect(profilesState.selectingProfileId).toBe(THORIN.ProfileId);
		release();
		await pending;
		expect(profilesState.selectingProfileId).toBeNull();
	});

	it('ignores a second select while one is still in flight', async () => {
		let release = (): void => {};
		request.mockReturnValueOnce(new Promise((resolve) => (release = () => resolve(undefined))));

		const first = selectProfile(THORIN.ProfileId);
		await expect(selectProfile(BALIN.ProfileId)).resolves.toBe(false);

		expect(request).toHaveBeenCalledTimes(1);
		release();
		await first;
	});

	it('records the selection so a reconnect can restore it', async () => {
		request.mockResolvedValue({ $type: 'SelectProfileResponse', Id: 1 });

		await selectProfile(THORIN.ProfileId);

		// Outside the session scope on purpose: the reset is what destroys the
		// evidence of what the session was, and the replay needs it afterwards.
		resetSessionState();
		expect(profilesState.selectedProfileId).toBeNull();
		expect(sessionIntent.profileId).toBe(THORIN.ProfileId);
	});
});

/*
 * The bug this whole mechanism exists for. Rejecting a pending request only
 * schedules a microtask, so the close handler's reset runs first and the
 * request's catch runs second — which used to leave `status` on 'error' after
 * the reset had put it back to 'idle'. The page's only load trigger fires on
 * 'idle', so the list never loaded again for the life of the page.
 */
describe('a connection dropping under a request', () => {
	function dropDuring<T>(promise: Promise<T>): Promise<T> {
		resetSessionState();
		connection.generation++;
		return promise;
	}

	it('leaves the list reloadable rather than stuck on an error', async () => {
		let fail = (): void => {};
		request.mockReturnValueOnce(
			new Promise((_, reject) => (fail = () => reject(new Error('WebSocket closed'))))
		);

		const loading = loadProfiles();
		expect(profilesState.status).toBe('loading');

		// The socket drops: the reset lands first, the rejection second.
		const dropped = dropDuring(loading);
		fail();
		await dropped;

		expect(profilesState.status).toBe('idle');
		expect(profilesState.error).toBeNull();

		// And the next attempt actually runs, which is what was impossible before.
		request.mockResolvedValueOnce(listResponse([THORIN]));
		await loadProfiles();
		expect(profilesState.status).toBe('loaded');
		expect(profilesState.profiles).toEqual([THORIN]);
	});

	it('discards a list that arrives after the connection carrying it died', async () => {
		let land = (): void => {};
		request.mockReturnValueOnce(
			new Promise((resolve) => (land = () => resolve(listResponse([THORIN]))))
		);

		const loading = loadProfiles();
		const dropped = dropDuring(loading);
		land();
		await dropped;

		// A success is as unwritable as a failure once its connection is gone: the
		// list belongs to a session that no longer exists.
		expect(profilesState.profiles).toEqual([]);
		expect(profilesState.status).toBe('idle');
	});

	it('does not surface a dead connection failure on the create form', async () => {
		let fail = (): void => {};
		request.mockReturnValueOnce(
			new Promise((_, reject) => (fail = () => reject(new Error('WebSocket closed'))))
		);

		const creating = createProfile('Thorin');
		const dropped = dropDuring(creating);
		fail();

		await expect(dropped).resolves.toBe(false);
		expect(profilesState.createError).toBeNull();
		expect(profilesState.creating).toBe(false);
	});

	it('does not surface a dead connection failure on a select', async () => {
		let fail = (): void => {};
		request.mockReturnValueOnce(
			new Promise((_, reject) => (fail = () => reject(new Error('WebSocket closed'))))
		);

		const selecting = selectProfile(THORIN.ProfileId);
		const dropped = dropDuring(selecting);
		fail();

		await expect(dropped).resolves.toBe(false);
		expect(profilesState.selectError).toBeNull();
		expect(profilesState.selectedProfileId).toBeNull();
	});
});
