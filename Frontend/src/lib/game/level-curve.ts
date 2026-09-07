/*
 * Port of Backend/LevelCurve.cs. The wire only ever carries a skill's
 * cumulative XP, so "how far into this level" has to be derived here from the
 * same curve. level-curve.spec.ts pins the C# fixtures so the two cannot drift.
 */
export const MAX_LEVEL = 50;

/** XP for level 2. Each later level adds BASE * R^(L-2). */
const BASE = 895;
/** Per-level growth rate. */
const R = 1.13;
/** The backend stores XP as int32 and clamps the curve to it. */
const INT32_MAX = 2147483647;

// C# Math.Round defaults to MidpointRounding.ToEven, whereas Math.round here
// rounds a half up. Only exact midpoints differ, but a single one would shift
// every cumulative threshold after it.
function roundHalfEven(value: number): number {
	const floor = Math.floor(value);
	const fraction = value - floor;
	if (fraction < 0.5) {
		return floor;
	}
	if (fraction > 0.5) {
		return floor + 1;
	}
	return floor % 2 === 0 ? floor : floor + 1;
}

/** The per-level cost the backend sums: round(BASE * R^(L-1)). */
function levelCost(level: number): number {
	return roundHalfEven(BASE * Math.pow(R, level - 1));
}

/** XP_FOR_LEVEL[L - 1] is the cumulative XP needed to be level L; level 1 is 0. */
const XP_FOR_LEVEL: number[] = [];
{
	let cumulative = 0;
	for (let level = 1; level <= MAX_LEVEL; level++) {
		XP_FOR_LEVEL.push(cumulative);
		cumulative += levelCost(level);
	}
}

/** Cumulative XP needed to be `level`. */
export function xpForLevel(level: number): number {
	if (level < 1) {
		return 0;
	}
	if (level <= MAX_LEVEL) {
		return XP_FOR_LEVEL[level - 1];
	}
	// Past the configured cap the backend keeps the same geometric per-level
	// cost, so this does too — including summing from R^(MaxLevel) onwards.
	let xp = XP_FOR_LEVEL[MAX_LEVEL - 1];
	for (let l = MAX_LEVEL + 1; l <= level; l++) {
		xp += levelCost(l);
		if (xp >= INT32_MAX) {
			return INT32_MAX;
		}
	}
	return Math.min(xp, INT32_MAX);
}

/** The level a player with `xp` has reached, 1 to MAX_LEVEL. */
export function levelFromXp(xp: number): number {
	let low = 0;
	let high = MAX_LEVEL - 1;
	while (low < high) {
		const mid = low + Math.floor((high - low + 1) / 2);
		if (xp >= XP_FOR_LEVEL[mid]) {
			low = mid;
		} else {
			high = mid - 1;
		}
	}
	return XP_FOR_LEVEL[low] <= xp ? low + 1 : 1;
}

export interface LevelProgress {
	level: number;
	/** XP earned within the current level. */
	into: number;
	/** XP the current level spans; `into` reaches it at the next level. */
	span: number;
}

/**
 * Where `xp` sits within its level. The backend sends the level it computed
 * alongside the XP, and passing that in keeps the badge and the bar agreeing
 * with the server even if this port were ever a rounding off.
 */
export function levelProgress(xp: number, level = levelFromXp(xp)): LevelProgress {
	const from = xpForLevel(level);
	const span = xpForLevel(level + 1) - from;
	// Clamped both ways: at MAX_LEVEL the XP keeps growing past a level that
	// never comes, and the bar should read full rather than overflow.
	const into = Math.max(0, Math.min(xp - from, span));
	return { level, into, span };
}
