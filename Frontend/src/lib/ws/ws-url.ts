import { browser, dev } from '$app/environment';
import { env } from '$env/dynamic/public';

/** Backend address in `bun run dev`, matching Backend/Properties/launchSettings.json. */
export const DEFAULT_WS_URL = 'ws://localhost:5066/ws';

/** Query parameter that sets — or, when empty, clears — the endpoint override. */
export const WS_URL_OVERRIDE_PARAM = 'ws';

/** localStorage key the override survives a reload in. */
export const WS_URL_OVERRIDE_KEY = 'openidle:ws-url';

/** Where the chosen URL came from. The two override values are worth logging. */
export type WsUrlSource = 'query-override' | 'stored-override' | 'configured' | 'dev-default';

export interface WsUrlInput {
	/** PUBLIC_WS_URL as configured for this deployment, if it is set. */
	readonly configured: string | undefined;
	/**
	 * Whether this build honours an override at all. Deployments that must only
	 * ever talk to their own backend — production — leave this false, which makes
	 * an override link inert rather than merely discouraged.
	 */
	readonly overrideEnabled: boolean;
	/** An override stored by an earlier visit, or null if none is stored. */
	readonly storedOverride: string | null;
	/**
	 * The override parameter on this navigation. `null` means it was absent —
	 * distinct from `''`, which is a developer asking to clear the stored one.
	 */
	readonly requestedOverride: string | null;
	/** Whether the localhost fallback applies, i.e. this is `vite dev`. */
	readonly dev: boolean;
}

export interface WsUrlResolution {
	readonly url: string;
	readonly source: WsUrlSource;
	/**
	 * What storage should hold afterwards; null means the key must be removed.
	 * Returned rather than written so the decision stays pure and testable.
	 */
	readonly override: string | null;
	/** Problems worth surfacing. Never fatal — a bad override falls back. */
	readonly warnings: readonly string[];
}

type OverrideParse =
	| { readonly kind: 'clear' }
	| { readonly kind: 'url'; readonly url: string }
	| { readonly kind: 'invalid' };

/**
 * Only ws:// and wss:// are accepted. A pasted http:// address is the likely
 * mistake, and letting it through would hand `new WebSocket()` something it
 * throws on much later, far from the query parameter that caused it.
 */
function parseOverride(raw: string | null): OverrideParse {
	if (raw === null) {
		return { kind: 'clear' };
	}
	const trimmed = raw.trim();
	if (trimmed === '' || trimmed === 'reset') {
		return { kind: 'clear' };
	}
	let parsed: URL;
	try {
		parsed = new URL(trimmed);
	} catch {
		return { kind: 'invalid' };
	}
	if (parsed.protocol !== 'ws:' && parsed.protocol !== 'wss:') {
		return { kind: 'invalid' };
	}
	return { kind: 'url', url: parsed.href };
}

/**
 * The whole endpoint decision, as a pure function of its inputs.
 *
 * Order: an override supplied on this navigation, then one stored by an earlier
 * one, then the deployment's own PUBLIC_WS_URL, then the dev fallback. The
 * override half is skipped entirely unless the build opted into it.
 *
 * A malformed override is ignored rather than fatal — resolution continues to
 * the next source — because a typo in a query parameter should not take the
 * client down, and because the warning says plainly what was dropped.
 *
 * @throws when nothing resolves, which outside development is a deployment
 * that never had its PUBLIC_WS_URL set. Failing here beats silently pointing
 * every client at localhost.
 */
export function selectWsUrl(input: WsUrlInput): WsUrlResolution {
	const warnings: string[] = [];
	let override: string | null = null;
	let source: WsUrlSource | null = null;

	if (input.overrideEnabled) {
		const requested = parseOverride(input.requestedOverride);

		// An absent parameter leaves an earlier override in force, and an invalid
		// one falls back to it too, so a typo costs nothing beyond the warning. An
		// explicitly empty parameter is the one case that must not consult storage:
		// clearing the override is exactly what it was typed to do.
		let consultStored = input.requestedOverride === null;

		if (requested.kind === 'url') {
			override = requested.url;
			source = 'query-override';
		} else if (requested.kind === 'invalid') {
			warnings.push(
				`Ignoring ?${WS_URL_OVERRIDE_PARAM}=${input.requestedOverride}: expected a ws:// or wss:// URL.`
			);
			consultStored = true;
		}

		if (source === null && consultStored) {
			const stored = parseOverride(input.storedOverride);
			if (stored.kind === 'url') {
				override = stored.url;
				source = 'stored-override';
			} else if (stored.kind === 'invalid') {
				warnings.push(
					`Discarding the stored WebSocket override ${input.storedOverride}: expected a ws:// or wss:// URL.`
				);
			}
		}
	}

	if (source !== null && override !== null) {
		return { url: override, source, override, warnings };
	}

	// Reached when the parameter cleared the override, when none applied, or when
	// overrides are off — in every case nothing should stay stored.
	if (input.configured) {
		return { url: input.configured, source: 'configured', override: null, warnings };
	}
	if (input.dev) {
		return { url: DEFAULT_WS_URL, source: 'dev-default', override: null, warnings };
	}

	console.warn(
		'PUBLIC_WS_URL is not set; refusing to fall back to the development default outside development.'
	);
	throw new Error('PUBLIC_WS_URL must be configured outside development');
}

/**
 * Returns the URL that would be used with no override in place: the configured
 * `PUBLIC_WS_URL`, or the dev fallback. Throws outside development when no
 * configuration is present — a missing value is a deployment error.
 */
export function defaultWsUrl(): string {
	const configured = env.PUBLIC_WS_URL;
	if (configured) {
		return configured;
	}
	if (dev) {
		return DEFAULT_WS_URL;
	}
	console.warn(
		'PUBLIC_WS_URL is not set; refusing to fall back to the development default outside development.'
	);
	throw new Error('PUBLIC_WS_URL must be configured outside development');
}

/**
 * Reading `window.localStorage` — not just using it — throws in a browser with
 * site data blocked, so even the handle has to be taken defensively.
 */
function openStorage(): Storage | null {
	try {
		return window.localStorage;
	} catch {
		return null;
	}
}

function persistOverride(storage: Storage, override: string | null): void {
	try {
		if (override === null) {
			storage.removeItem(WS_URL_OVERRIDE_KEY);
		} else {
			storage.setItem(WS_URL_OVERRIDE_KEY, override);
		}
	} catch {
		// A session that cannot persist the override still honours it for this
		// page; losing it on reload beats failing the connection over it.
	}
}

/**
 * Reads the stored WebSocket override, guarded against environments where
 * localStorage is blocked or absent.
 */
export function readStoredWsUrl(): string | null {
	const storage = openStorage();
	if (!storage) {
		return null;
	}
	try {
		return storage.getItem(WS_URL_OVERRIDE_KEY);
	} catch {
		return null;
	}
}

/**
 * Persists (or clears, when `null`) the WebSocket override, guarded against
 * environments where localStorage is blocked or absent.
 */
export function writeStoredWsUrl(url: string | null): void {
	const storage = openStorage();
	if (storage) {
		persistOverride(storage, url);
	}
}

/**
 * Gathers the ambient inputs — SvelteKit's env, the URL, storage — and hands
 * them to {@link selectWsUrl}. Everything conditional lives there; this only
 * decides what the current environment actually offers.
 *
 * Overrides are a development affordance: they let the deployed dev frontend be
 * pointed at whichever backend the developer is running locally, which no single
 * deployment-wide PUBLIC_WS_URL can do. Production leaves
 * PUBLIC_ALLOW_WS_OVERRIDE unset and so only ever reaches its own backend.
 */
export function resolveWsUrl(): string {
	const overrideEnabled = browser && (dev || env.PUBLIC_ALLOW_WS_OVERRIDE === 'true');
	const storage = overrideEnabled ? openStorage() : null;

	const resolution = selectWsUrl({
		configured: env.PUBLIC_WS_URL,
		dev,
		overrideEnabled,
		requestedOverride: overrideEnabled
			? new URLSearchParams(window.location.search).get(WS_URL_OVERRIDE_PARAM)
			: null,
		storedOverride: storage?.getItem(WS_URL_OVERRIDE_KEY) ?? null
	});

	for (const warning of resolution.warnings) {
		console.warn(warning);
	}
	if (storage) {
		persistOverride(storage, resolution.override);
	}
	if (resolution.source === 'query-override' || resolution.source === 'stored-override') {
		// Loud on purpose: a client talking to an unexpected backend is otherwise
		// a confusing bug rather than an obvious local setting.
		console.info(
			`OpenIdle: WebSocket override in effect — connecting to ${resolution.url}. ` +
				`Clear it with ?${WS_URL_OVERRIDE_PARAM}=`
		);
	}

	return resolution.url;
}
