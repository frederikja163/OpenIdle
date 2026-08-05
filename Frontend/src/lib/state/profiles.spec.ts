import { beforeEach, describe, expect, it, vi } from 'vitest';

const { request } = vi.hoisted(() => ({ request: vi.fn() }));

// The module's onClose reset is not exercised here: it registers behind
// `if (browser)`, which is false in this node project.
vi.mock('$lib/ws/client', () => ({
	getWsClient: () => ({ request, onClose: () => () => {} })
}));

const { createProfile, loadProfiles, profilesState, selectProfile, validateProfileName } =
	await import('$lib/state/profiles.svelte');

const THORIN = { Name: 'Thorin', ProfileId: '11111111-1111-1111-1111-111111111111' };
const BALIN = { Name: 'Balin', ProfileId: '22222222-2222-2222-2222-222222222222' };

function listResponse(profiles: (typeof THORIN)[]) {
	return { $type: 'ListProfilesResponse', Id: 1, Profiles: profiles };
}

beforeEach(() => {
	request.mockReset();
	profilesState.status = 'idle';
	profilesState.profiles = [];
	profilesState.error = null;
	profilesState.creating = false;
	profilesState.createError = null;
	profilesState.selectedProfileId = null;
	profilesState.selectingProfileId = null;
	profilesState.selectError = null;
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
});
