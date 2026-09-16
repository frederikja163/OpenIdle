import { getApiUrl } from '$lib/ws/client';
import { levelsUrl } from '$lib/ws/ws-url';

/*
 * The XP-per-level table the backend computes, fetched over HTTP rather than
 * carried on the socket. The curve is static tuning, so it sits on the plumbing
 * side like the version footer, and the fetch starts at startup so it is in hand
 * long before a profile is loaded. The board's XP bars read it reactively.
 */

const REQUEST_TIMEOUT_MS = 5_000;

export const levelCurveState = $state({
	status: 'loading' as 'loading' | 'loaded' | 'failed',
	/**
	 * Cumulative XP per level, index = level. Null before the first answer and
	 * after a failure.
	 */
	levels: null as number[] | null
});

// The endpoint the last ask went to, so a caller can skip re-asking a backend
// that has already answered.
let askedUrl: string | null = null;

// Bumped by every ask, so an answer from a backend the client has since left is
// discarded rather than painted over the current one.
let sequence = 0;
let inflight: AbortController | null = null;

/**
 * Fetches the curve unless it has already been fetched from the backend the
 * client is currently pointed at. For startup, and for a socket opening on a
 * possibly different backend.
 */
export function ensureLevelCurve(): Promise<void> {
	if (askedUrl === currentLevelsUrl() && levelCurveState.status !== 'failed') {
		return Promise.resolve();
	}
	return loadLevelCurve();
}

/** Fetches the curve now, superseding any ask in flight. */
export async function loadLevelCurve(): Promise<void> {
	const url = currentLevelsUrl();
	const ask = ++sequence;
	inflight?.abort();
	const controller = new AbortController();
	inflight = controller;

	// A known table stays visible while the same backend is asked again; another
	// backend's table would be a lie in the meantime.
	if (url !== askedUrl) {
		levelCurveState.levels = null;
	}
	askedUrl = url;
	levelCurveState.status = 'loading';
	try {
		const levels = await fetchLevels(url, controller);
		if (ask !== sequence) {
			return;
		}
		levelCurveState.levels = levels;
		levelCurveState.status = levels === null ? 'failed' : 'loaded';
	} catch {
		if (ask !== sequence) {
			return;
		}
		levelCurveState.levels = null;
		levelCurveState.status = 'failed';
	}
}

function currentLevelsUrl(): string {
	return levelsUrl(getApiUrl());
}

async function fetchLevels(url: string, controller: AbortController): Promise<number[] | null> {
	// A deadline so a backend that never answers cannot leave the board unable
	// to measure a level for as long as the browser's own timeout.
	const timer = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
	try {
		const response = await fetch(url, { signal: controller.signal });
		if (!response.ok) {
			throw new Error(`Levels endpoint answered ${response.status}`);
		}
		return normalize(await response.json());
	} finally {
		clearTimeout(timer);
	}
}

/** The wire shape is a JSON array of cumulative XP indexed by level. */
function normalize(body: unknown): number[] | null {
	if (!Array.isArray(body) || body.length < 2) {
		return null;
	}
	const levels = body.filter(
		(value): value is number => typeof value === 'number' && Number.isInteger(value) && value >= 0
	);
	return levels.length === body.length ? levels : null;
}
