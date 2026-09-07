import { describe, expect, it } from 'vitest';
import { levelFromXp, levelProgress, MAX_LEVEL, xpForLevel } from './level-curve';

// The fixtures are the backend's own (tests/OpenIdle.Tests/LevelCurveTests.cs),
// so a port that rounds or sums differently fails here rather than on a bar.
const INT32_MAX = 2147483647;

describe('xpForLevel', () => {
	it.each([
		[1, 0],
		[2, 895],
		[15, 31219],
		[25, 122465],
		[30, 231433],
		[50, 2739261]
	])('needs the curve requirement to be level %i', (level, xp) => {
		expect(xpForLevel(level)).toBe(xp);
	});

	it('clamps to int32 once the requirement exceeds it', () => {
		expect(xpForLevel(INT32_MAX)).toBe(INT32_MAX);
	});

	it('treats anything below level 1 as free', () => {
		expect(xpForLevel(0)).toBe(0);
		expect(xpForLevel(-3)).toBe(0);
	});
});

describe('levelFromXp', () => {
	it.each([
		[0, 1],
		[894, 1],
		[895, 2],
		[2739260, 49],
		[2739261, 50],
		[INT32_MAX, 50]
	])('maps %i xp to level %i', (xp, level) => {
		expect(levelFromXp(xp)).toBe(level);
	});

	it('never drops below level 1', () => {
		expect(levelFromXp(-1)).toBe(1);
	});
});

describe('levelProgress', () => {
	it('starts a fresh skill at the bottom of level 1', () => {
		expect(levelProgress(0)).toEqual({ level: 1, into: 0, span: 895 });
	});

	it('measures xp within the level and the span to the next', () => {
		// 895 opens level 2 and level 3 costs round(895 * 1.13) = 1011 more.
		expect(levelProgress(1000)).toEqual({ level: 2, into: 105, span: 1011 });
	});

	it('takes the level the server computed rather than re-deriving it', () => {
		expect(levelProgress(1000, 1)).toEqual({ level: 1, into: 895, span: 895 });
	});

	it('reads full at the cap instead of overflowing', () => {
		const capped = levelProgress(INT32_MAX);

		expect(capped.level).toBe(MAX_LEVEL);
		expect(capped.into).toBe(capped.span);
	});

	it('starts the cap at zero like any other level', () => {
		expect(levelProgress(2739261).into).toBe(0);
	});
});
