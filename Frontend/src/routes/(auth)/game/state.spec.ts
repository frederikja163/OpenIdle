import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('$lib/ws/client', async () => (await import('$lib/state/test-support')).clientModule);

const { gameState } = await import('$lib/state/game.svelte');
const { resetSessionState } = await import('$lib/state/session.svelte');
const { BoardState } = await import('./state.svelte');

const PROFILE = '11111111-1111-1111-1111-111111111111';

/*
 * The board is a projection of the game store, so every case here sets the
 * store directly and reads the projection — the store's own behaviour is
 * covered in $lib/state/game.spec.ts.
 */
describe('BoardState', () => {
	beforeEach(() => {
		resetSessionState();
	});

	it('dresses the skills with the level curve and the catalog', () => {
		gameState.skills = { Mining: { profileId: PROFILE, skillId: 'Mining', xp: 895, level: 2 } };
		const board = new BoardState();

		expect(board.skills[0]).toMatchObject({
			id: 'Mining',
			name: 'Mining',
			level: 2,
			xp: 0,
			xpMax: 1011
		});
		// A skill the server has no row for yet starts at the bottom of level 1.
		expect(board.skills[1]).toMatchObject({ id: 'LumberJacking', level: 1, xp: 0, xpMax: 895 });
		expect(board.totalLevel).toBe(4);
	});

	it('lists only the items actually held, in catalog order', () => {
		gameState.items = { Balsa: 4, Tin: 3, Copper: 0 };
		const board = new BoardState();

		expect(board.inventory.map((item) => item.id)).toEqual(['Tin', 'Balsa']);
		expect(board.inventory[0]).toMatchObject({ name: 'Tin Ore', count: 3, kind: 'res' });
	});

	it('follows the running skill until the rail is picked', () => {
		const board = new BoardState();
		expect(board.selectedSkill).toBe('Mining');

		gameState.running = { activityId: 'ChopBalsa', startedAt: Date.now() };
		expect(board.selectedSkill).toBe('LumberJacking');
		expect(board.running).toMatchObject({
			skill: 'LumberJacking',
			id: 'ChopBalsa',
			name: 'Chop Balsa Log',
			ms: 8000
		});

		board.activeSkill = 'Crafting';
		expect(board.selectedSkill).toBe('Crafting');
	});

	it('shows nothing running for an activity it cannot draw', () => {
		gameState.running = { activityId: 'None', startedAt: Date.now() };
		const board = new BoardState();

		expect(board.running).toBeNull();
		expect(board.progress).toBe(0);
	});
});

describe('BoardState clock', () => {
	let board: InstanceType<typeof BoardState>;
	let teardown: () => void = () => {};

	beforeEach(() => {
		vi.useFakeTimers();
		resetSessionState();
		board = new BoardState();
	});

	afterEach(() => {
		teardown();
		vi.useRealTimers();
	});

	it('does not tick while nothing runs', () => {
		teardown = board.run();

		vi.advanceTimersByTime(1000);
		expect(board.progress).toBe(0);
		expect(vi.getTimerCount()).toBe(0);
	});

	it('advances the meter with the clock and holds it full until the payout', () => {
		// Mining tin takes 8000ms.
		gameState.running = { activityId: 'MineTin', startedAt: Date.now() };
		teardown = board.run();

		vi.advanceTimersByTime(4000);
		expect(board.progress).toBe(50);

		// Well past the deadline the bar waits at exactly full...
		vi.advanceTimersByTime(5000);
		expect(board.progress).toBe(100);

		// ...and the payout moving the start is what snaps it back to zero.
		gameState.running = { activityId: 'MineTin', startedAt: Date.now() };
		expect(board.progress).toBe(0);
	});
});
