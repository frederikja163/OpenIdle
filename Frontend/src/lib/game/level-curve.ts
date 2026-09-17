/*
 * Where an amount of XP sits within a level, from the backend's own table.
 *
 * The cumulative XP per level is fetched over HTTP (state/level-curve.svelte.ts)
 * rather than re-derived here: the curve is geometric tuning the backend owns,
 * and a second implementation is a second thing to drift. These helpers only
 * look boundaries up in the array the backend sent.
 */
export interface LevelProgress {
	level: number;
	/** XP earned within the current level. */
	into: number;
	/** XP the current level spans; `into` reaches it at the next level. */
	span: number;
}

/**
 * Where `xp` sits within `level`, using the backend's cumulative table (index =
 * level). `levels` is null until it has been fetched, in which case the level
 * still shows but the bar has nothing to measure.
 *
 * The backend sends the level it computed, and passing that in keeps the badge
 * and the bar agreeing with it. The table runs one entry past the cap so the
 * final level's span is still available; there, XP keeps growing past a level
 * that never comes and the bar reads full rather than overflowing.
 */
export function levelProgress(
	levels: readonly number[] | null,
	xp: number,
	level: number
): LevelProgress {
	if (!levels || levels.length === 0) {
		return { level, into: 0, span: 0 };
	}
	const from = levels[level] ?? levels[levels.length - 1];
	const to = levels[level + 1] ?? from;
	const span = Math.max(0, to - from);
	const into = Math.max(0, Math.min(xp - from, span));
	return { level, into, span };
}
