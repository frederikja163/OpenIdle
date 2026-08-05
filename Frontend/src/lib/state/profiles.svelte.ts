import { browser } from '$app/environment';
import { getWsClient } from '$lib/ws/client';
import type { ProfileDto } from '$lib/ws/protocol';

export type ProfilesStatus = 'idle' | 'loading' | 'loaded' | 'error';

export const MAX_PROFILE_NAME_LENGTH = 30;

// Creating and selecting each carry their own in-flight flag and error so that
// neither disturbs `status`/`error`, which mean "the ListProfiles fetch" — the
// page renders the list, the create card and every card footer at once.
export const profilesState = $state({
	status: 'idle' as ProfilesStatus,
	profiles: [] as ProfileDto[],
	error: null as string | null,
	creating: false,
	createError: null as string | null,
	// The socket owns the selection and never reports it back, so this is the
	// client's only record of it — and it is worth exactly as much as the
	// connection that produced it.
	selectedProfileId: null as string | null,
	selectingProfileId: null as string | null,
	selectError: null as string | null
});

function toMessage(error: unknown): string {
	return error instanceof Error ? error.message : String(error);
}

/*
 * Mirrors ProfileService.CreateProfileAsync in the backend so a name the server
 * would refuse never costs a round trip. The character class is deliberately
 * plain ASCII rather than \w, which matches char.IsAsciiLetterOrDigit and, unlike
 * \w, excludes the underscore.
 *
 * Uniqueness is not checkable here — the unique index on Profile.Name is the only
 * authority — so a duplicate comes back as a server error like any other.
 */
export function validateProfileName(name: string): string | null {
	if (name.length === 0) {
		return 'Enter a profile name.';
	}
	if (name.length > MAX_PROFILE_NAME_LENGTH) {
		return `Profile name must be at most ${MAX_PROFILE_NAME_LENGTH} characters.`;
	}
	if (!/^[A-Za-z0-9]+$/.test(name)) {
		return 'Profile name must be letters and digits only.';
	}
	return null;
}

export async function loadProfiles(): Promise<void> {
	if (profilesState.status === 'loading') {
		return;
	}
	profilesState.status = 'loading';
	profilesState.error = null;
	try {
		const response = await getWsClient().request('ListProfilesRequest', {});
		profilesState.profiles = response.Profiles;
		profilesState.status = 'loaded';
	} catch (error) {
		profilesState.status = 'error';
		profilesState.error = toMessage(error);
	}
}

/** Resolves true once the profile exists and the list has caught up. */
export async function createProfile(name: string): Promise<boolean> {
	if (profilesState.creating) {
		return false;
	}
	// Trimmed before validating rather than after: the server's alphanumeric check
	// rejects a stray trailing space rather than ignoring it, so surrounding
	// whitespace is dropped instead of turned into an error.
	const trimmed = name.trim();
	const invalid = validateProfileName(trimmed);
	if (invalid) {
		profilesState.createError = invalid;
		return false;
	}
	profilesState.creating = true;
	profilesState.createError = null;
	try {
		await getWsClient().request('CreateProfileRequest', { Name: trimmed });
	} catch (error) {
		profilesState.createError = toMessage(error);
		return false;
	} finally {
		profilesState.creating = false;
	}
	// CreateProfileResponse carries no profile, so refetching is the only way to
	// learn the new ProfileId. A failure here is a list failure and surfaces
	// through `status`, not through the create form: the profile was still made.
	await loadProfiles();
	return true;
}

/**
 * Points the socket at a profile. Only one select runs at a time: the backend
 * handles frames FIFO per connection, so two overlapping selects would both
 * apply and leave `selectedProfileId` disagreeing with the socket.
 */
export async function selectProfile(profileId: string): Promise<boolean> {
	if (profilesState.selectingProfileId !== null) {
		return false;
	}
	profilesState.selectingProfileId = profileId;
	profilesState.selectError = null;
	try {
		await getWsClient().request('SelectProfileRequest', { ProfileId: profileId });
		profilesState.selectedProfileId = profileId;
		return true;
	} catch (error) {
		// SelectProfileAsync throws before it assigns, so a rejection leaves the
		// socket on whatever profile it already had: don't clear selectedProfileId.
		profilesState.selectError = toMessage(error);
		return false;
	} finally {
		profilesState.selectingProfileId = null;
	}
}

// The backend session is the connection, so profiles fetched over a dead socket
// belong to a dead session: reset to 'idle' rather than 'loggedOut' so the next
// login refetches instead of showing the previous user's list. The selection goes
// with it, for the same reason — it only ever lived on that socket.
if (browser) {
	getWsClient().onClose(() => {
		profilesState.status = 'idle';
		profilesState.profiles = [];
		profilesState.error = null;
		profilesState.creating = false;
		profilesState.createError = null;
		profilesState.selectedProfileId = null;
		profilesState.selectingProfileId = null;
		profilesState.selectError = null;
	});
}
