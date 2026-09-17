import { getApiUrl } from '$lib/ws/client';
import { schemaUrl } from '$lib/ws/ws-url';
import type { ProtocolSchema } from './schema';
import { SchemaError, toProtocolSchema } from './specToSchema';

/*
 * The request catalogue the console builds forms from, fetched from whichever
 * backend the app is pointed at.
 *
 * It used to be compiled in, emitted from types.xml at build time, which meant
 * the page described this repository's contract and not the one on the other end
 * of the socket — a mismatch nothing could detect, so it was only ever a warning
 * on screen. Asking the backend removes the question: the catalogue is the
 * contract the backend was built from.
 *
 * The cost is that the console now has a loading state and a way to fail, which
 * is why this mirrors $lib/state/version.svelte.ts closely — it already answers
 * every question a backend fetch raises here: a URL that changes under it, a
 * slow reply from the backend that was just navigated away from, and a request
 * that never answers at all.
 */

const REQUEST_TIMEOUT_MS = 5_000;

export const schemaState = $state({
	/**
	 * Whether the pointed-at backend has answered, and how. Starts as loading so a
	 * render before the first ask looks the same as one with an ask in flight.
	 */
	status: 'loading' as 'loading' | 'loaded' | 'failed',
	/** The catalogue; null before the first answer and after a failure. */
	protocol: null as ProtocolSchema | null,
	/**
	 * Why the last ask failed, for the panel to show. A 404 from a backend built
	 * before /schema existed and a contract this build cannot read are different
	 * problems for whoever is on this page, and 'failed' tells them apart from
	 * neither.
	 */
	error: null as string | null
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
 * Asks the pointed-at backend for its contract unless it has already been asked
 * and has not failed. For the console mounting, or the backend URL changing.
 */
export function ensureProtocolSchema(): Promise<void> {
	if (askedUrl === currentSchemaUrl() && schemaState.status !== 'failed') {
		return Promise.resolve();
	}
	return loadProtocolSchema();
}

/** Asks the pointed-at backend for its contract now, superseding any ask in flight. */
export async function loadProtocolSchema(): Promise<void> {
	const url = currentSchemaUrl();
	const ask = ++sequence;
	inflight?.abort();
	const controller = new AbortController();
	inflight = controller;

	// A catalogue belongs to the backend it came from: keeping the old one on
	// screen while a different backend is asked would offer requests that may not
	// exist there. Asking the same backend again leaves it in place.
	if (url !== askedUrl) {
		schemaState.protocol = null;
	}
	askedUrl = url;
	schemaState.status = 'loading';
	schemaState.error = null;
	try {
		const protocol = await fetchSchema(url, controller);
		if (ask !== sequence) {
			return;
		}
		schemaState.protocol = protocol;
		schemaState.status = 'loaded';
	} catch (error) {
		if (ask !== sequence) {
			return;
		}
		schemaState.protocol = null;
		schemaState.status = 'failed';
		schemaState.error = message(error);
	}
}

function currentSchemaUrl(): string {
	return schemaUrl(getApiUrl());
}

async function fetchSchema(url: string, controller: AbortController): Promise<ProtocolSchema> {
	// A deadline so a backend that never answers cannot leave the console loading
	// for as long as the browser's own timeout.
	const timer = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
	try {
		const response = await fetch(url, { signal: controller.signal });
		if (!response.ok) {
			throw new Error(`${url} answered ${response.status}.`);
		}
		return toProtocolSchema(await response.json());
	} finally {
		clearTimeout(timer);
	}
}

function message(error: unknown): string {
	if (error instanceof SchemaError) {
		return error.message;
	}
	// Anything else is the fetch itself — a refused connection, the abort above, or
	// a body that is not JSON. The browser's own wording is the most specific thing
	// available, and prefixing the address is what makes it actionable.
	return `Could not read ${askedUrl}: ${error instanceof Error ? error.message : String(error)}`;
}
