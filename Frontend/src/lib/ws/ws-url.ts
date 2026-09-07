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

export interface ApiUrlInput {
	/** PUBLIC_API_URL as configured for this deployment, if it is set. */
	readonly configured: string | undefined;
	/** The WebSocket URL the client is pointed at, which the HTTP side is derived from otherwise. */
	readonly wsUrl: string;
	/**
	 * Whether `wsUrl` is an override rather than the deployment's own backend.
	 * An override names a whole backend, so its HTTP side has to follow it: the
	 * configured API URL belongs to the backend that was just overridden away.
	 */
	readonly wsOverridden: boolean;
}

export interface ApiUrlResolution {
	readonly url: string;
	readonly source: 'configured' | 'derived';
	/** Problems worth surfacing. Never fatal — a bad value falls back to derivation. */
	readonly warnings: readonly string[];
}

/**
 * The HTTP base of the backend, as a pure function of its inputs: the
 * configured `PUBLIC_API_URL`, unless the socket is overridden or nothing is
 * configured, in which case it is derived from the WebSocket URL with
 * {@link apiUrlFromWsUrl}. Deriving is the fallback rather than the rule so a
 * backend whose HTTP side lives somewhere other than next to its socket can
 * say so explicitly.
 */
export function selectApiUrl(input: ApiUrlInput): ApiUrlResolution {
	const warnings: string[] = [];
	if (!input.wsOverridden && input.configured) {
		const configured = parseApiUrl(input.configured);
		if (configured !== null) {
			return { url: configured, source: 'configured', warnings };
		}
		warnings.push(
			`Ignoring PUBLIC_API_URL=${input.configured}: expected an http:// or https:// URL. Deriving the API address from the WebSocket URL instead.`
		);
	}
	return { url: apiUrlFromWsUrl(input.wsUrl), source: 'derived', warnings };
}

/**
 * Only http:// and https:// are accepted, since fetch speaks nothing else. The
 * likely mistake is pasting the ws:// address here, which would otherwise fail
 * far away from the variable that caused it.
 */
function parseApiUrl(raw: string): string | null {
	let parsed: URL;
	try {
		parsed = new URL(raw.trim());
	} catch {
		return null;
	}
	if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
		return null;
	}
	parsed.search = '';
	parsed.hash = '';
	return stripTrailingSlash(parsed.href);
}

/**
 * The HTTP base a backend's WebSocket URL implies: the same host with the
 * scheme swapped and the final path segment (the `/ws`) removed, so a backend
 * mounted under a prefix (wss://host/api/ws) keeps that prefix.
 */
export function apiUrlFromWsUrl(wsUrl: string): string {
	const parsed = new URL(wsUrl);
	parsed.protocol = parsed.protocol === 'wss:' ? 'https:' : 'http:';
	parsed.pathname = parsed.pathname.replace(/\/[^/]*\/?$/, '');
	parsed.search = '';
	parsed.hash = '';
	return stripTrailingSlash(parsed.href);
}

/** The backend's `GET /version`, under whichever API base it is reached at. */
export function versionUrl(apiUrl: string): string {
	return `${stripTrailingSlash(apiUrl)}/version`;
}

function stripTrailingSlash(url: string): string {
	return url.replace(/\/+$/, '');
}

/**
 * Gathers the ambient inputs for {@link selectApiUrl}: the deployment's
 * `PUBLIC_API_URL`, and the WebSocket URL the client is actually on, which the
 * caller knows and this module does not.
 */
export function resolveApiUrl(wsUrl: string, wsOverridden: boolean): string {
	const resolution = selectApiUrl({ configured: env.PUBLIC_API_URL, wsUrl, wsOverridden });
	for (const warning of resolution.warnings) {
		console.warn(warning);
	}
	return resolution.url;
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
