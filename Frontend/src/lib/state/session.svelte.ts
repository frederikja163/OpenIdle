import { getWsClient, type WsStatus } from '$lib/ws/client';

/*
 * The backend session IS the websocket connection, so every store that holds
 * server data holds it on loan from a connection that can vanish. This module
 * owns the three things that follow from that, so no individual store has to
 * remember them:
 *
 *  - state scoped to the connection, which empties itself when the session ends;
 *  - a runner that refuses to write a result belonging to a dead connection;
 *  - the intent needed to put a reconnected socket back the way it was.
 */

/** How the connection itself is doing. Outlives any one session, so not scoped. */
export const connectionState = $state({
	status: 'closed' as WsStatus
});

/**
 * What a reconnect has to replay. Deliberately plain, non-reactive and outside
 * the session scope: it is the one thing that must survive the reset, because
 * the reset is what destroys the evidence of what the session was.
 */
export const sessionIntent = {
	loggedIn: false,
	profileId: null as string | null
};

export function forgetSessionIntent(): void {
	sessionIntent.loggedIn = false;
	sessionIntent.profileId = null;
}

// An array rather than a Set because this is a plain append-only registry, not
// reactive state: every sessionState() call contributes its own closure, so
// there is nothing to deduplicate.
const resets: (() => void)[] = [];

/**
 * State whose lifetime is the connection. Every field returns to the value its
 * initialiser gives it when the session ends, so a store never maintains a list
 * of fields to clear — the bug being that a field added to the state and
 * forgotten in the reset survives a logout and leaks into the next session.
 */
export function sessionState<T extends object>(create: () => T): T {
	const state = $state(create());
	resets.push(() => Object.assign(state, create()));
	return state;
}

/** Ends the session: empties every store and forgets what to replay. */
export function endSession(): void {
	for (const reset of resets) {
		reset();
	}
	forgetSessionIntent();
}

/** Empties every store but keeps the intent, so a reconnect can restore them. */
export function resetSessionState(): void {
	for (const reset of resets) {
		reset();
	}
}

export type SessionOutcome = 'ok' | 'failed' | 'stale';

export interface SessionHandlers<T> {
	ok: (value: T) => void;
	fail: (message: string) => void;
}

/**
 * Runs a request and applies its outcome only if the connection that carried it
 * is still the current one.
 *
 * Rejecting a pending request merely schedules a microtask, so a caller's catch
 * always runs *after* the close handlers that reset the store — and would
 * otherwise paint a dead connection's failure over the fresh state. Returning
 * 'stale' rather than calling either handler is what makes that unwritable, and
 * putting it here rather than in each store is what makes it unforgettable.
 */
export async function sessionRun<T>(
	run: () => Promise<T>,
	handlers: SessionHandlers<T>
): Promise<SessionOutcome> {
	const client = getWsClient();
	const generation = client.generation;
	try {
		const value = await run();
		if (client.generation !== generation) {
			return 'stale';
		}
		handlers.ok(value);
		return 'ok';
	} catch (error) {
		if (client.generation !== generation) {
			return 'stale';
		}
		handlers.fail(error instanceof Error ? error.message : String(error));
		return 'failed';
	}
}
