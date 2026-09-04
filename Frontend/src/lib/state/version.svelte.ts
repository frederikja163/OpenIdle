import { resolveWsUrl, versionHttpUrl } from '$lib/ws/ws-url';
import type { BuildInfo } from '$lib/utils/version';

/*
 * Which builds are running: this bundle's, inlined at build time, and whatever
 * backend it points at, fetched over HTTP. Plain module state rather than
 * sessionState(): a build is not session data and must outlive a session drop.
 *
 * The backend half is deliberately not the socket: asking the version is
 * plumbing, and it should not pull a WebSocket open on a page that has not
 * asked for one. It is keyed off the ws URL so an override — ?ws=, or a change
 * made on the debug page — moves it to the same backend as everything else.
 */

const REQUEST_TIMEOUT_MS = 5_000;

export const versionState = $state({
	frontend: __OPENIDLE_BUILD__ as BuildInfo,
	/** Whether the connected backend has answered, and how. */
	status: 'idle' as 'idle' | 'loading' | 'loaded' | 'failed',
	/** The build the backend reported, or null before it has or after a failure. */
	backend: null as BuildInfo | null
});

// The URL the version was last asked of. One value per backend actually
// reached: the first mount asks once even though the open it causes fires the
// footer's effect again, while a changed URL — an override on the debug page —
// asks anew.
let requestedUrl: string | null = null;

// Bumped by every ask. An answer that lands after a newer ask superseded it is
// discarded, so a slow /version from an old backend cannot paint over the new.
let requestSequence = 0;

/**
 * Drops the cached answer and the once-per-URL guard, so the next ask fetches
 * whether or not the backend's address changed. Nothing in the app needs this
 * today — it exists for tests, which have to retire one backend before asking
 * another — and as a hook for a future invalidation.
 */
export function forgetBackendVersion(): void {
	requestedUrl = null;
	requestSequence++;
	versionState.backend = null;
	versionState.status = 'idle';
}

/** Asks the pointed-at backend for its build, once per URL. */
export async function loadBackendVersion(): Promise<void> {
	const url = versionHttpUrl(resolveWsUrl());
	if (requestedUrl === url) {
		return;
	}
	requestedUrl = url;
	const sequence = ++requestSequence;
	versionState.status = 'loading';
	try {
		const build = await fetchBuild(url);
		if (sequence !== requestSequence) {
			return;
		}
		versionState.backend = build;
		versionState.status = 'loaded';
	} catch {
		if (sequence !== requestSequence) {
			return;
		}
		versionState.backend = null;
		versionState.status = 'failed';
	}
}

async function fetchBuild(url: string): Promise<BuildInfo> {
	// An AbortController so a backend that never answers cannot leave the footer
	// spinning on '…' for as long as the browser's own timeout.
	const controller = new AbortController();
	const timer = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
	try {
		const response = await fetch(url, { signal: controller.signal });
		if (!response.ok) {
			throw new Error(`Version endpoint answered ${response.status}`);
		}
		return normalize(await response.json());
	} finally {
		clearTimeout(timer);
	}
}

/** The wire shape is a JSON object; anything else or missing is a local build. */
function normalize(body: unknown): BuildInfo {
	const record = body !== null && typeof body === 'object' ? (body as Record<string, unknown>) : {};
	return {
		commit: typeof record.commit === 'string' && record.commit !== '' ? record.commit : null,
		commitTime: typeof record.commitTime === 'number' ? record.commitTime : null
	};
}
