import { describe, expect, it } from 'vitest';
import { ACTIVITY_DATA, type ActivityName } from '$lib/ws/protocol';
import { actionById, actionsBySkill, canAfford, SKILL_ORDER, toGameAction } from './catalog';

// Against the real contract rather than a fixture: what these pin is that every
// activity types.xml declares today can be drawn as a card.
describe('toGameAction', () => {
	it('maps every declared activity', () => {
		for (const name of Object.keys(ACTIVITY_DATA) as ActivityName[]) {
			expect(() => toGameAction(name)).not.toThrow();
		}
	});

	it('reads the skill, yield, xp and duration off the rewards', () => {
		expect(toGameAction('MineTin')).toMatchObject({
			id: 'MineTin',
			skill: 'Mining',
			name: 'Mine Tin Ore',
			ms: 8000,
			xp: 200,
			qty: 2
		});
		expect(toGameAction('MineTin').lockedAt).toBeUndefined();
	});

	it('gates on the requirement for its own skill', () => {
		expect(toGameAction('MineSteel')).toMatchObject({ lockedAt: 41, ms: 28000 });
	});

	it('turns costs into inputs', () => {
		const handle = toGameAction('CraftBalsaHandle');

		expect(handle).toMatchObject({ name: 'Craft Balsa Handle', skill: 'Crafting', qty: 10 });
		expect(handle.inputs).toEqual([expect.objectContaining({ id: 'Balsa', qty: 1 })]);
	});

	it('names a crafted tool after the tool', () => {
		expect(toGameAction('CraftTinPickaxeHead').name).toBe('Craft Tin Pickaxe Head');
	});
});

describe('actionsBySkill', () => {
	it('gives every skill on the rail something to do', () => {
		for (const skill of SKILL_ORDER) {
			expect(actionsBySkill[skill].length).toBeGreaterThan(0);
		}
	});

	it('finds an action by its id and nothing for None', () => {
		expect(actionById('ChopBalsa')?.name).toBe('Chop Balsa Log');
		expect(actionById('None')).toBeUndefined();
	});
});

describe('canAfford', () => {
	it('is true for an action without costs', () => {
		expect(canAfford('MineTin', {})).toBe(true);
	});

	it('needs every cost covered', () => {
		expect(canAfford('CraftBalsaHandle', {})).toBe(false);
		expect(canAfford('CraftBalsaHandle', { Balsa: 1 })).toBe(true);
	});

	it('cannot afford None', () => {
		expect(canAfford('None', {})).toBe(false);
	});
});
