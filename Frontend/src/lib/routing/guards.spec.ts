import { describe, expect, it } from 'vitest';
import { requiresProfile } from './guards';

describe('requiresProfile', () => {
	it('claims the board', () => {
		expect(requiresProfile('/game')).toBe(true);
	});

	it('claims what is nested under the board', () => {
		expect(requiresProfile('/game/skills')).toBe(true);
	});

	it('leaves the rest of the app alone', () => {
		expect(requiresProfile('/profiles')).toBe(false);
		expect(requiresProfile('/login')).toBe(false);
	});

	// A prefix test without the separator would gate every route that merely
	// starts with the same letters.
	it('does not claim a sibling whose name starts the same way', () => {
		expect(requiresProfile('/gamepad')).toBe(false);
	});
});
