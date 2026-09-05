import Axe from '@lucide/svelte/icons/axe';
import Hammer from '@lucide/svelte/icons/hammer';
import Minus from '@lucide/svelte/icons/minus';
import Mountain from '@lucide/svelte/icons/mountain';
import Pickaxe from '@lucide/svelte/icons/pickaxe';
import Trees from '@lucide/svelte/icons/trees';
import type { Rarity } from '$lib/components/game/ItemArt.svelte';
import {
	ACTIVITY_DATA,
	type ActivityId,
	type ActivityName,
	type ItemId,
	type ItemReward,
	type XpReward
} from '$lib/ws/protocol';
import type { GameAction, IconComponent, ItemKind, PlayableItemId, PlayableSkillId } from './types';

/*
 * Display data for the ids the backend deals in. Both tables are checked
 * against the generated enums, so a skill or item added to types.xml without an
 * entry here fails `bun run check` instead of rendering a card with no art —
 * and a removed one fails the same way rather than lingering.
 */
export interface SkillMeta {
	name: string;
	icon: IconComponent;
}

export interface ItemMeta {
	name: string;
	glyph: IconComponent;
	rarity: Rarity;
	kind: ItemKind;
}

/** Declaration order is the rail order. */
export const SKILLS = {
	Mining: { name: 'Mining', icon: Pickaxe },
	LumberJacking: { name: 'Lumberjacking', icon: Trees },
	Crafting: { name: 'Crafting', icon: Hammer }
} satisfies Record<PlayableSkillId, SkillMeta>;

export const SKILL_ORDER = Object.keys(SKILLS) as PlayableSkillId[];

/** Tiers 1–5 (Tin/Balsa through Steel/Oak) climb the rarity ladder together. */
const TIER_RARITY: Rarity[] = ['common', 'common', 'uncommon', 'rare', 'epic'];

function ore(name: string, tier: number): ItemMeta {
	return { name, glyph: Mountain, rarity: TIER_RARITY[tier - 1], kind: 'res' };
}

function wood(name: string, tier: number): ItemMeta {
	return { name, glyph: Trees, rarity: TIER_RARITY[tier - 1], kind: 'res' };
}

function tool(name: string, glyph: IconComponent, tier: number): ItemMeta {
	return { name, glyph, rarity: TIER_RARITY[tier - 1], kind: 'tool' };
}

/** Declaration order is the inventory order. */
export const ITEMS = {
	Tin: ore('Tin Ore', 1),
	Copper: ore('Copper Ore', 2),
	Bronze: ore('Bronze Ore', 3),
	Iron: ore('Iron Ore', 4),
	Steel: ore('Steel Ore', 5),
	Balsa: wood('Balsa Log', 1),
	Pine: wood('Pine Log', 2),
	Cedar: wood('Cedar Log', 3),
	Cherry: wood('Cherry Log', 4),
	Oak: wood('Oak Log', 5),
	BalsaHandle: tool('Balsa Handle', Minus, 1),
	PineHandle: tool('Pine Handle', Minus, 2),
	CedarHandle: tool('Cedar Handle', Minus, 3),
	CherryHandle: tool('Cherry Handle', Minus, 4),
	OakHandle: tool('Oak Handle', Minus, 5),
	TinHammerHead: tool('Tin Hammer Head', Hammer, 1),
	CopperHammerHead: tool('Copper Hammer Head', Hammer, 2),
	BronzeHammerHead: tool('Bronze Hammer Head', Hammer, 3),
	IronHammerHead: tool('Iron Hammer Head', Hammer, 4),
	SteelHammerHead: tool('Steel Hammer Head', Hammer, 5),
	TinPickaxeHead: tool('Tin Pickaxe Head', Pickaxe, 1),
	CopperPickaxeHead: tool('Copper Pickaxe Head', Pickaxe, 2),
	BronzePickaxeHead: tool('Bronze Pickaxe Head', Pickaxe, 3),
	IronPickaxeHead: tool('Iron Pickaxe Head', Pickaxe, 4),
	SteelPickaxeHead: tool('Steel Pickaxe Head', Pickaxe, 5),
	TinAxeHead: tool('Tin Axe Head', Axe, 1),
	CopperAxeHead: tool('Copper Axe Head', Axe, 2),
	BronzeAxeHead: tool('Bronze Axe Head', Axe, 3),
	IronAxeHead: tool('Iron Axe Head', Axe, 4),
	SteelAxeHead: tool('Steel Axe Head', Axe, 5)
} satisfies Record<PlayableItemId, ItemMeta>;

export const INVENTORY_ORDER = Object.keys(ITEMS) as PlayableItemId[];

export function itemMeta(id: ItemId): ItemMeta {
	if (id === 'None') {
		throw new Error('The None item has no catalog entry.');
	}
	return ITEMS[id];
}

/** The verbs an activity name may open with, in the order they are tried. */
const VERBS = ['Mine', 'Chop', 'Craft'];

function activityLabel(name: ActivityName, primary: PlayableItemId): string {
	const verb = VERBS.find((candidate) => name.startsWith(candidate));
	const item = ITEMS[primary].name;
	return verb ? `${verb} ${item}` : item;
}

/**
 * Dresses a generated activity definition as a card. The card's skill, art and
 * label are all read off the definition's rewards, so a new activity needs
 * nothing here beyond its items being in ITEMS.
 */
export function toGameAction(name: ActivityName): GameAction {
	const definition = ACTIVITY_DATA[name];
	const xp = definition.rewards.find((reward): reward is XpReward => reward.kind === 'xp');
	// The guaranteed item, not a bonus table roll: the card shows what a
	// completion always pays.
	const primary = definition.rewards.find(
		(reward): reward is ItemReward => reward.kind === 'item' && reward.weight === null
	);
	if (!xp || xp.skill === 'None' || !primary || primary.item === 'None') {
		throw new Error(`Activity '${name}' has no xp reward and item reward to draw a card from.`);
	}
	const skill = xp.skill;
	const art = ITEMS[primary.item];
	const requirement = definition.requirements.find((level) => level.skill === skill)?.count;
	return {
		id: name,
		skill,
		name: activityLabel(name, primary.item),
		glyph: art.glyph,
		rarity: art.rarity,
		ms: definition.time * 1000,
		xp: xp.count,
		qty: primary.count,
		// Level 1 gates nothing; the card would only print a lock for it.
		lockedAt: requirement !== undefined && requirement > 1 ? requirement : undefined,
		inputs:
			definition.costs.length === 0
				? undefined
				: definition.costs.map((cost) => {
						const meta = itemMeta(cost.item);
						return {
							id: cost.item,
							name: meta.name,
							glyph: meta.glyph,
							rarity: meta.rarity,
							qty: cost.count
						};
					})
	};
}

/** Every action, grouped by skill in the order types.xml declares them. */
export const actionsBySkill: Record<PlayableSkillId, GameAction[]> = Object.fromEntries(
	SKILL_ORDER.map((id) => [id, [] as GameAction[]])
) as Record<PlayableSkillId, GameAction[]>;

const actionsById = new Map<ActivityName, GameAction>();

for (const name of Object.keys(ACTIVITY_DATA) as ActivityName[]) {
	const action = toGameAction(name);
	actionsBySkill[action.skill].push(action);
	actionsById.set(name, action);
}

export function actionById(id: ActivityId): GameAction | undefined {
	return id === 'None' ? undefined : actionsById.get(id);
}

/** Mirrors ActivityService.CanAffordActivityAsync: every cost covered by the pack. */
export function canAfford(activityId: ActivityId, items: Partial<Record<ItemId, number>>): boolean {
	if (activityId === 'None') {
		return false;
	}
	return ACTIVITY_DATA[activityId].costs.every((cost) => (items[cost.item] ?? 0) >= cost.count);
}
