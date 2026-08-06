import { getWsClient } from '$lib/ws/client';
import { loadProfiles, profilesState, replayProfileSelection } from './profiles.svelte';
import { replayLogin } from './user.svelte';

/**
 * Connects the socket to the stores. Called once from the root layout rather
 * than run as an import side effect, so the order below is stated in one place
 * and so tests can drive it without a browser.
 *
 * The replay lives here instead of being registered piecemeal by each store
 * because its order is the whole point — the socket has to be logged in before
 * it can be pointed at a profile — and because a store cannot know what the
 * ones after it need.
 */
export function wireSession(): void {
	const client = getWsClient();

	client.setResume(async (send) => {
		await replayLogin(send);
		await replayProfileSelection(send);
		// Deliberately not awaited. It goes through the ordinary request path,
		// which waits on the gate this replay is still holding, so awaiting it here
		// would deadlock. Queuing it instead means it runs the instant the gate
		// lowers, which is exactly when the list is worth fetching again.
		void loadProfiles();
	});

	// TODO: nothing sends this yet — the backend branch that does is unmerged.
	// It carries the full list rather than a delta, so it can simply overwrite.
	client.onEvent('ProfilesChangedEvent', (event) => {
		profilesState.profiles = event.Profiles;
		profilesState.status = 'loaded';
	});
}
