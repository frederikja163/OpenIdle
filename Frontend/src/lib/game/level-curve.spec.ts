import { describe, expect, it } from 'vitest';
import { levelFromXp, levelProgress, MAX_LEVEL, xpForLevel } from './level-curve';

// The fixtures are the backend's own (tests/OpenIdle.Tests/LevelCurveTests.cs),
// so a port that rounds or sums differently fails here rather than on a bar.
const INT32_MAX = 2147483647;

describe('xpForLevel', () => {
	it.each([
		[0, 0],
		[1, 895],
		[14, 31219],
		[24, 122465],
		[29, 231433],
		[49, 2739261],
		[50, 3096260]
	])('needs the curve requirement to be level %i', (level, xp) => {
		expect(xpForLevel(level)).toBe(xp);
	});

	it('clamps to int32 once the requirement exceeds it', () => {
		expect(xpForLevel(INT32_MAX)).toBe(INT32_MAX);
	});

	it('treats anything below level 0 as free', () => {
		expect(xpForLevel(0)).toBe(0);
		expect(xpForLevel(-3)).toBe(0);
	});
});

describe('levelFromXp', () => {
	it.each([
		[0, 0],
		[894, 0],
		[895, 1],
		[2739260, 48],
		[2739261, 49],
		[INT32_MAX, 50]
	])('maps %i xp to level %i', (xp, level) => {
		expect(levelFromXp(xp)).toBe(level);
	});

	it('never drops below level 0', () => {
		expect(levelFromXp(-1)).toBe(0);
	});
});

describe('levelProgress', () => {
	it('starts a fresh skill at the bottom of level 0', () => {
		expect(levelProgress(0)).toEqual({ level: 0, into: 0, span: 895 });
	});

	it('measures xp within the level and the span to the next', () => {
		// 895 opens level 1 and level 2 costs round(895 * 1.13) = 1011 more.
		expect(levelProgress(1000)).toEqual({ level: 1, into: 105, span: 1011 });
	});

	it('takes the level the server computed rather than re-deriving it', () => {
		expect(levelProgress(1000, 0)).toEqual({ level: 0, into: 895, span: 895 });
	});

	it('reads full at the cap instead of overflowing', () => {
		const capped = levelProgress(INT32_MAX);

		expect(capped.level).toBe(MAX_LEVEL);
		expect(capped.into).toBe(capped.span);
	});

	it('starts the cap at zero like any other level', () => {
		expect(levelProgress(xpForLevel(MAX_LEVEL)).into).toBe(0);
	});
});
