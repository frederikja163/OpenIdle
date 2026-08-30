import type { FrameKind } from './traffic.svelte';

export const FILTERABLE_KINDS = [
	'request',
	'response',
	'event'
] as const satisfies readonly FrameKind[];
export type FilterableKind = (typeof FILTERABLE_KINDS)[number];

// Per-browser and persistent, like the backend override in $lib/ws/client:
// which kinds a developer wants to see is a decision about this machine.
const STORAGE_KEY = 'openidle.debug.visibleKinds';

export const preferences = $state({ visibleKinds: readVisibleKinds() });

export function isVisible(kind: FrameKind): boolean {
	return kind === 'unknown' || (preferences.visibleKinds as readonly FrameKind[]).includes(kind);
}

// Written through here rather than mirrored by an $effect: stored and live
// values can never disagree, nothing runs on first render, and a module needs
// no root to do it in.
export function toggleKind(kind: FilterableKind): void {
	preferences.visibleKinds = preferences.visibleKinds.includes(kind)
		? preferences.visibleKinds.filter((candidate) => candidate !== kind)
		: [...preferences.visibleKinds, kind];
	writeVisibleKinds(preferences.visibleKinds);
}

function isFilterable(value: unknown): value is FilterableKind {
	return FILTERABLE_KINDS.includes(value as FilterableKind);
}

// Guarded like client.ts's helpers: localStorage is absent in SSR/unit tests
// and throws in a browser that blocks site data.
function readVisibleKinds(): FilterableKind[] {
	try {
		const stored = localStorage.getItem(STORAGE_KEY);
		const parsed: unknown = stored === null ? null : JSON.parse(stored);
		return Array.isArray(parsed) ? parsed.filter(isFilterable) : [...FILTERABLE_KINDS];
	} catch {
		return [...FILTERABLE_KINDS];
	}
}

function writeVisibleKinds(kinds: readonly FilterableKind[]): void {
	try {
		localStorage.setItem(STORAGE_KEY, JSON.stringify(kinds));
	} catch {
		// Live log still filtered; it just will not survive the reload.
	}
}
