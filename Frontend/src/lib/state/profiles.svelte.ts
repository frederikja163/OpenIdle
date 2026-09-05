import { getWsClient, type PrivilegedSend } from '$lib/ws/client';
import type { ProfileDto } from '$lib/ws/protocol';
import { sessionIntent, sessionRun, sessionState } from './session.svelte';

export type ProfilesStatus = 'idle' | 'loading' | 'loaded' | 'error';

export const MAX_PROFILE_NAME_LENGTH = 30;

// Creating and selecting each carry their own in-flight flag and error so that
// neither disturbs `status`/`error`, which mean "the ListProfiles fetch" — the
// page renders the list, the create card and every card footer at once.
export const profilesState = sessionState(() => ({
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
}));

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
	await sessionRun(() => getWsClient().request('ListProfilesRequest', {}), {
		ok: (response) => {
			profilesState.profiles = response.profiles;
			profilesState.status = 'loaded';
		},
		fail: (message) => {
			profilesState.status = 'error';
			profilesState.error = message;
		}
	});
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
	const outcome = await sessionRun(
		() => getWsClient().request('CreateProfileRequest', { name: trimmed }),
		{
			ok: () => {},
			fail: (message) => {
				profilesState.createError = message;
			}
		}
	);
	if (outcome !== 'ok') {
		// On 'stale' the reset already cleared this; assigning false again is
		// harmless and keeps the failure paths identical.
		profilesState.creating = false;
		return false;
	}
	// CreateProfileResponse carries no profile, so refetching is the only way to
	// learn the new profileId. A failure here is a list failure and surfaces
	// through `status`, not through the create form: the profile was still made.
	await loadProfiles();
	// Held until the refetch lands, so the form cannot be submitted a second time
	// against a list that has not caught up yet.
	profilesState.creating = false;
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
	const outcome = await sessionRun(
		() => getWsClient().request('SelectProfileRequest', { profileId }),
		{
			ok: () => {
				profilesState.selectedProfileId = profileId;
				sessionIntent.profileId = profileId;
			},
			// SelectProfileAsync throws before it assigns, so a rejection leaves the
			// socket on whatever profile it already had: don't clear the selection.
			fail: (message) => {
				profilesState.selectError = message;
			}
		}
	);
	profilesState.selectingProfileId = null;
	return outcome === 'ok';
}

/** Puts a reconnected socket back on the profile the old one was pointed at. */
export async function replayProfileSelection(send: PrivilegedSend): Promise<void> {
	const profileId = sessionIntent.profileId;
	if (profileId === null) {
		return;
	}
	try {
		await send('SelectProfileRequest', { profileId });
	} catch (error) {
		// Unlike selectProfile(), a refusal here leaves the socket on no profile at
		// all — the connection is new. Forgetting the intent is what stops a profile
		// the backend will never accept again (a deleted one) from failing the same
		// way on every future reconnect and aborting the rest of the replay with it.
		sessionIntent.profileId = null;
		// The intent is not reactive, so this is the only trace of the refusal a
		// page can react to — the game board reads it to stop waiting for a
		// profile that is never coming back.
		profilesState.selectError = error instanceof Error ? error.message : String(error);
		throw error;
	}
	profilesState.selectedProfileId = profileId;
}
