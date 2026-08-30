import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const STORAGE_KEY = 'openidle.debug.visibleKinds';

// A stand-in for localStorage without needing a browser: the real thing's
// storage is keyed by the document's origin, which node has no notion of.
function mapBackedStorage(): Storage {
	const store = new Map<string, string>();
	return {
		get length() {
			return store.size;
		},
		clear: () => store.clear(),
		getItem: (key) => store.get(key) ?? null,
		key: (index) => [...store.keys()][index] ?? null,
		removeItem: (key) => store.delete(key),
		setItem: (key, value) => store.set(key, String(value))
	};
}

beforeEach(() => {
	vi.stubGlobal('localStorage', mapBackedStorage());
	// The module reads storage at import time, so each case must start with a
	// fresh module instance or the stored value from the last one leaks in.
	vi.resetModules();
});

afterEach(() => {
	vi.unstubAllGlobals();
});

async function loadPreferences() {
	return await import('$lib/debug/preferences.svelte');
}

describe('visible kind preferences', () => {
	it('defaults to every kind when nothing is stored', async () => {
		const { FILTERABLE_KINDS, isVisible, preferences } = await loadPreferences();

		expect(preferences.visibleKinds).toEqual([...FILTERABLE_KINDS]);
		expect(isVisible('unknown')).toBe(true);
	});

	it('shows only the stored kinds, and unknown is never hidden', async () => {
		localStorage.setItem(STORAGE_KEY, '["event"]');

		const { isVisible, preferences } = await loadPreferences();

		expect(preferences.visibleKinds).toEqual(['event']);
		expect(isVisible('event')).toBe(true);
		expect(isVisible('request')).toBe(false);
		expect(isVisible('unknown')).toBe(true);
	});

	it('flips a kind and persists the change as JSON', async () => {
		const { isVisible, toggleKind } = await loadPreferences();

		toggleKind('event');
		expect(isVisible('event')).toBe(false);
		expect(localStorage.getItem(STORAGE_KEY)).toBe('["request","response"]');

		toggleKind('event');
		expect(isVisible('event')).toBe(true);
		expect(localStorage.getItem(STORAGE_KEY)).toBe('["request","response","event"]');
	});

	it('falls back to every kind for stored garbage', async () => {
		localStorage.setItem(STORAGE_KEY, 'garbage');

		const { FILTERABLE_KINDS, preferences } = await loadPreferences();

		expect(preferences.visibleKinds).toEqual([...FILTERABLE_KINDS]);
	});

	it('drops unknown names from a stored list', async () => {
		localStorage.setItem(STORAGE_KEY, '["bogus","event"]');

		const { preferences } = await loadPreferences();

		expect(preferences.visibleKinds).toEqual(['event']);
	});

	it('defaults, without throwing, when localStorage is unavailable', async () => {
		vi.unstubAllGlobals();
		vi.resetModules();

		const { FILTERABLE_KINDS, preferences } = await loadPreferences();

		expect(preferences.visibleKinds).toEqual([...FILTERABLE_KINDS]);
	});
});
