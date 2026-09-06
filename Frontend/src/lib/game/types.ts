import type { ActionInput } from '$lib/components/game/ActionCard.svelte';
import type { Rarity } from '$lib/components/game/ItemArt.svelte';
import type { IconComponent } from '$lib/components/icon';
import type { ActivityId, ActivityName, ItemId, SkillId } from '$lib/ws/protocol';

/*
 * What the board's components render. The wire carries ids and counts; the
 * catalog dresses them in names, glyphs and rarities, and the game store adds
 * what only the client knows — the running action and the last payout.
 */

export type ItemKind = 'res' | 'tool';

/** Every generated enum leads with a `None` the game never plays. */
export type PlayableSkillId = Exclude<SkillId, 'None'>;
export type PlayableItemId = Exclude<ItemId, 'None'>;

export interface Skill {
	id: PlayableSkillId;
	name: string;
	icon: IconComponent;
	level: number;
	/** XP earned within the current level. */
	xp: number;
	/** XP the current level spans. */
	xpMax: number;
}

/** One level gate on an action. The skill need not be the one the action trains. */
export interface SkillRequirement {
	skill: PlayableSkillId;
	level: number;
}

/**
 * An input the catalog built. `ActionInput` types its id as a plain string
 * because the card is a design-system component that knows nothing about the
 * contract; here it is a real item id, so a lookup needs no cast.
 */
export type GameActionInput = ActionInput & { id: PlayableItemId };

export interface GameAction {
	id: ActivityName;
	skill: PlayableSkillId;
	name: string;
	glyph: IconComponent;
	rarity: Rarity;
	/** Time for one completion, in milliseconds. */
	ms: number;
	xp: number;
	/** How many of the item one completion yields, before any bonus drop. */
	qty: number;
	/** Every level gate the action carries. Level 1 gates nothing and is omitted. */
	requirements: SkillRequirement[];
	inputs?: GameActionInput[];
}

export interface InventoryItem {
	id: PlayableItemId;
	name: string;
	glyph: IconComponent;
	rarity: Rarity;
	count: number;
	kind: ItemKind;
}

/** One payout, as the difference it made. `key` only exists to remount the float. */
export interface BoardReward {
	action: ActivityId;
	key: number;
	xp: number;
	items: { itemId: ItemId; delta: number }[];
}
