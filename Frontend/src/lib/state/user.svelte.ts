import { getWsClient, WsError, type PrivilegedSend } from '$lib/ws/client';
import { endSession, sessionIntent, sessionState } from './session.svelte';

export type LoginStatus = 'loggedOut' | 'loggingIn' | 'loggedIn' | 'error';

// The backend session lives on the connection, so losing it logs us out: this
// empties itself when the session ends, without naming its own fields.
export const userState = sessionState(() => ({
	status: 'loggedOut' as LoginStatus,
	// TODO: LoginAsTestUserResponse carries no user id yet; populate this once
	// the backend returns one.
	userId: null as string | null,
	error: null as string | null
}));

/**
 * After a hot reload this module's state resets while the socket stays logged
 * in, so the backend's rejection actually means success.
 */
const ALREADY_LOGGED_IN = 'Already logged in.';

export async function ensureLoggedIn(): Promise<void> {
	if (userState.status === 'loggingIn' || userState.status === 'loggedIn') {
		return;
	}
	// Logging in is a fresh session rather than a recovery, so the client has to
	// be re-armed: close() shut it deliberately and it will not reconnect on its
	// own until someone says that shutdown is over.
	getWsClient().reopen();
	userState.status = 'loggingIn';
	userState.error = null;
	/*
	 * Deliberately not behind sessionRun, unlike every other request in the app.
	 * That guard drops a result whose connection has since died, because for a
	 * store holding server data the reset is a better answer than a dead
	 * connection's error. Logging in is the operation that *creates* the session,
	 * so it has no earlier state to protect and the failure is the whole news —
	 * discarding it would leave the login page saying nothing at all.
	 */
	try {
		await getWsClient().request('LoginAsTestUserRequest', {});
		userState.status = 'loggedIn';
		sessionIntent.loggedIn = true;
	} catch (error) {
		if (error instanceof WsError && error.message === ALREADY_LOGGED_IN) {
			userState.status = 'loggedIn';
			sessionIntent.loggedIn = true;
			return;
		}
		userState.status = 'error';
		userState.error = error instanceof Error ? error.message : String(error);
	}
}

/** Puts a reconnected socket back into the logged-in state the old one held. */
export async function replayLogin(send: PrivilegedSend): Promise<void> {
	if (!sessionIntent.loggedIn) {
		return;
	}
	try {
		await send('LoginAsTestUserRequest', {});
	} catch (error) {
		if (!(error instanceof WsError && error.message === ALREADY_LOGGED_IN)) {
			throw error;
		}
	}
	userState.status = 'loggedIn';
}

/**
 * The backend has no logout message — the session is the connection — so
 * dropping the socket is the logout.
 *
 * endSession() runs here rather than being left to the close handler, which
 * does not run at all when the socket is already gone: close() no-ops silently
 * in that case.
 */
export function logout(): void {
	getWsClient().close();
	endSession();
}
