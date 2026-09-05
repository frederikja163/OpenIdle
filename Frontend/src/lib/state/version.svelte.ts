import { getWsUrl } from '$lib/ws/client';
import { versionHttpUrl } from '$lib/ws/ws-url';
import type { BuildInfo } from '$lib/utils/version';

/*
 * Which builds are running: this bundle's, inlined at build time, and whatever
 * backend it points at, fetched over HTTP. Plain module state rather than
 * sessionState(): a build is not session data and must outlive a session drop.
 *
 * The backend half deliberately stays off the socket: asking for a build is
 * plumbing and must not pull a WebSocket open. The endpoint derives from the
 * URL the client is pointed at, so an override — ?ws=, or a change on the
 * debug page — moves the version fetch with everything else.
 */

const REQUEST_TIMEOUT_MS = 5_000;

export const versionState = $state({
	frontend: __OPENIDLE_BUILD__ as BuildInfo,
	/**
	 * Whether the pointed-at backend has answered, and how. Starts as loading so
	 * a render before the first ask looks the same as one with an ask in flight.
	 */
	status: 'loading' as 'loading' | 'loaded' | 'failed',
	/** The build the backend reported; null before the first answer and after a failure. */
	backend: null as BuildInfo | null
});

// The endpoint the last ask went to, so a mount can skip re-asking a backend
// that has already answered.
let askedUrl: string | null = null;

// Bumped by every ask. An answer whose ask has since been superseded is
// discarded, so a slow reply from a previous backend cannot paint over the
// current one, and a superseded ask's own timeout cannot fail a newer answer.
let sequence = 0;
let inflight: AbortController | null = null;

/**
 * Asks the pointed-at backend for its build unless it has already been asked
 * and has not failed. For a footer mounting, or the backend URL changing.
 */
export function ensureBackendVersion(): Promise<void> {
	if (askedUrl === currentVersionUrl() && versionState.status !== 'failed') {
		return Promise.resolve();
	}
	return loadBackendVersion();
}

/** Asks the pointed-at backend for its build now, superseding any ask in flight. */
export async function loadBackendVersion(): Promise<void> {
	const url = currentVersionUrl();
	const ask = ++sequence;
	inflight?.abort();
	const controller = new AbortController();
	inflight = controller;

	// A known value stays visible while the same backend is asked again; a
	// different backend's value would be a lie in the meantime.
	if (url !== askedUrl) {
		versionState.backend = null;
	}
	askedUrl = url;
	versionState.status = 'loading';
	try {
		const build = await fetchBuild(url, controller);
		if (ask !== sequence) {
			return;
		}
		versionState.backend = build;
		versionState.status = 'loaded';
	} catch {
		if (ask !== sequence) {
			return;
		}
		versionState.backend = null;
		versionState.status = 'failed';
	}
}

function currentVersionUrl(): string {
	return versionHttpUrl(getWsUrl());
}

async function fetchBuild(url: string, controller: AbortController): Promise<BuildInfo> {
	// A deadline so a backend that never answers cannot leave the footer on '…'
	// for as long as the browser's own timeout.
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
		commit: typeof record.commit === 'string' ? record.commit : null,
		commitTime: typeof record.commitTime === 'number' ? record.commitTime : null
	};
}
