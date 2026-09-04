import type { BuildInfo } from '$lib/utils/version';
import { getWsClient } from '$lib/ws/client';
import { sessionRun } from './session.svelte';

/*
 * Which builds are running: this bundle's, inlined at build time, and the
 * connected backend's, asked over the socket. Plain module state rather than
 * sessionState(): a build is not session data and must outlive a socket drop.
 */
export const versionState = $state({
	frontend: __OPENIDLE_BUILD__ as BuildInfo,
	/** Null until the connected backend has answered, and again once that connection is gone. */
	backend: null as BuildInfo | null
});

// The connection the version was last requested on. The generation bumps every
// time a connection is retired, so one request per value is one request per
// backend actually reached: the first mount asks once even though the open it
// causes fires the footer's effect again, while a reconnect — or the debug
// console pointing the app elsewhere — asks anew.
let requestedGeneration: number | null = null;

/** Asks the connected backend for its build, once per connection. Dials if closed. */
export async function loadBackendVersion(): Promise<void> {
	const client = getWsClient();
	if (requestedGeneration === client.generation) {
		return;
	}
	requestedGeneration = client.generation;
	await sessionRun(() => client.request('GetVersionRequest', {}), {
		ok: (response) => {
			versionState.backend = {
				commit: response.commit ?? null,
				commitTime: response.commitTime ?? null
			};
		},
		fail: () => {
			versionState.backend = null;
		}
	});
}

/**
 * Registered against the socket's close by wireSession(): a version belongs to
 * the connection that reported it, and the next one may reach another backend.
 */
export function forgetBackendVersion(): void {
	versionState.backend = null;
}
