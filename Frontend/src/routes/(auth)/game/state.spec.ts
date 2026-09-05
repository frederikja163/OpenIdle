import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { actions } from './data';
import { BoardState } from './state.svelte';

/*
 * The board's loop runner drives a setInterval, so the model's methods are the
 * only piece tested here — start()/stop() decide *what* is running, run() only
 * advances it. None of these cases arm the interval.
 */
const talc = actions.mining.find((action) => action.id === 'talc')!;
const gypsum = actions.mining.find((action) => action.id === 'gypsum')!;
const calcitepick = actions.crafting.find((action) => action.id === 'calcitepick')!;

describe('BoardState', () => {
	let board: BoardState;

	beforeEach(() => {
		board = new BoardState();
	});

	it('runs an action within the selected skill', () => {
		board.start(gypsum);

		expect(board.running).toMatchObject({ skill: 'mining', action: 'gypsum' });
		expect(board.progress).toBe(0);
	});

	it('does not stop the action already running when pressed again', () => {
		const before = board.running;
		board.start(talc);

		expect(board.running).toBe(before);
		expect(board.running?.action).toBe('talc');
	});

	it('switches to a different action instead', () => {
		board.start(gypsum);

		expect(board.running?.action).toBe('gypsum');
	});

	it('refuses an action whose material inputs are short', () => {
		// Calcite Pickaxe Head costs 6 calcite; the mock horde holds 4.
		board.start(calcitepick);

		expect(board.running?.action).toBe('talc');
	});

	it('stops the running action and resets its meter', () => {
		board.stop();

		expect(board.running).toBeNull();
		expect(board.progress).toBe(0);
	});
});

describe('BoardState run loop', () => {
	let board: BoardState;
	let teardown: () => void;

	beforeEach(() => {
		vi.useFakeTimers();
		board = new BoardState();
	});

	afterEach(() => {
		teardown?.();
		vi.useRealTimers();
	});

	it('holds a full bar, then completes and snaps back to zero', () => {
		// Talc is a 5000ms action: 50 ticks of 2% at a 100ms TICK_MS.
		board.start(talc);
		teardown = board.run();

		vi.advanceTimersByTime(4900);
		expect(board.progress).toBe(98);

		// The 50th tick clamps to exactly 100 instead of rolling over...
		vi.advanceTimersByTime(100);
		expect(board.progress).toBe(100);

		// ...and the next tick pays out and snaps the meter straight to zero.
		vi.advanceTimersByTime(100);
		expect(board.reward).not.toBeNull();
		expect(board.reward?.action).toBe('talc');
		expect(board.progress).toBe(0);
	});
});
