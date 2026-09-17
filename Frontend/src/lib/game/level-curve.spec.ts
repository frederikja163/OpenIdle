import { describe, expect, it } from 'vitest';
import { levelProgress } from './level-curve';

// A stand-in for the backend's table (index = level), short enough to read. The
// real values are pinned by LevelsHttpIntegrationTests on the backend; what is
// under test here is only how a boundary is looked up.
const LEVELS = [0, 100, 250, 450];

describe('levelProgress', () => {
	it('measures xp within the level and the span to the next', () => {
		expect(levelProgress(LEVELS, 50, 0)).toEqual({ level: 0, into: 50, span: 100 });
		expect(levelProgress(LEVELS, 175, 1)).toEqual({ level: 1, into: 75, span: 150 });
	});

	it('takes the level the server computed rather than re-deriving it', () => {
		// The same xp reads differently at the level the server named.
		expect(levelProgress(LEVELS, 130, 0)).toEqual({ level: 0, into: 100, span: 100 });
		expect(levelProgress(LEVELS, 130, 1)).toEqual({ level: 1, into: 30, span: 150 });
	});

	it('reads full at the cap instead of overflowing', () => {
		// The extra table entry past the last level is what makes the span work.
		expect(levelProgress(LEVELS, 100_000, 2)).toEqual({ level: 2, into: 200, span: 200 });
	});

	it('has nothing to measure before the table has loaded', () => {
		expect(levelProgress(null, 500, 3)).toEqual({ level: 3, into: 0, span: 0 });
	});

	it('never reports negative xp', () => {
		expect(levelProgress(LEVELS, -20, 1)).toEqual({ level: 1, into: 0, span: 150 });
	});
});
