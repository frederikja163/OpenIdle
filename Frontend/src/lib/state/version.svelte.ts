import { resolveWsUrl, versionHttpUrl } from '$lib/ws/ws-url';
import type { BuildInfo } from '$lib/utils/version';

/*
 * Which builds are running: this bundle's, inlined at build time, and whatever
 * backend it points at, fetched over HTTP. Plain module state rather than
 * sessionState(): a build is not session data and must outlive a session drop.
 *
 * The backend half deliberately stays off the socket: asking for a build is
 * plumbing and must not pull a WebSocket open. The endpoint derives from the
 * ws URL, so an override — ?ws=, or a change on the debug page — moves the
 * version fetch with everything else.
 */

const REQUEST_TIMEOUT_MS = 5_000;

export const versionState = $state({
	frontend: __OPENIDLE_BUILD__ as BuildInfo,
	/** Whether the connected backend has answered, and how. */
	status: 'idle' as 'idle' | 'loading' | 'loaded' | 'failed',
	/** The build the backend reported, or null before it has or after a failure. */
	backend: null as BuildInfo | null
});

/** Asks the pointed-at backend for its build. Each ask is a fresh fetch. */
export async function loadBackendVersion(): Promise<void> {
	versionState.status = 'loading';
	try {
		versionState.backend = await fetchBuild(versionHttpUrl(resolveWsUrl()));
		versionState.status = 'loaded';
	} catch {
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
