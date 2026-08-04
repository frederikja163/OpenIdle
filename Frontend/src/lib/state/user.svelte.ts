import { browser } from '$app/environment';
import { getWsClient, WsError } from '$lib/ws/client';

export type LoginStatus = 'loggedOut' | 'loggingIn' | 'loggedIn' | 'error';

export const userState = $state({
	status: 'loggedOut' as LoginStatus,
	// TODO: LoginAsTestUserResponse carries no user id yet; populate this once
	// the backend returns one.
	userId: null as string | null,
	error: null as string | null
});

export async function ensureLoggedIn(): Promise<void> {
	if (userState.status === 'loggingIn' || userState.status === 'loggedIn') {
		return;
	}
	userState.status = 'loggingIn';
	userState.error = null;
	try {
		await getWsClient().request('LoginAsTestUserRequest', {});
		userState.status = 'loggedIn';
	} catch (error) {
		// After a hot reload this module's state resets while the socket stays
		// logged in, so the backend's rejection actually means success.
		if (error instanceof WsError && error.message === 'Already logged in.') {
			userState.status = 'loggedIn';
			return;
		}
		userState.status = 'error';
		userState.error = error instanceof Error ? error.message : String(error);
	}
}

/**
 * The backend has no logout message — the session is the connection — so
 * dropping the socket is the logout.
 *
 * The state is reset here rather than left to the `onClose` handler below,
 * which fires a tick later and cannot be relied on at all when the socket is
 * already gone: `close()` no-ops silently in that case.
 */
export function logout(): void {
	getWsClient().close();
	userState.status = 'loggedOut';
	userState.userId = null;
	userState.error = null;
}

// The backend session lives on the connection, so losing it logs us out.
if (browser) {
	getWsClient().onClose(() => {
		userState.status = 'loggedOut';
		userState.userId = null;
	});
}
