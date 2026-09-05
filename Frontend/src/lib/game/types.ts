import type { Component } from 'svelte';
import type { ActionInput } from '$lib/components/game/ActionCard.svelte';
import type { Rarity } from '$lib/components/game/ItemArt.svelte';
import type { ActivityId, ActivityName, ItemId, SkillId } from '$lib/ws/protocol';

/*
 * What the board's components render. The wire carries ids and counts; the
 * catalog dresses them in names, glyphs and rarities, and the game store adds
 * what only the client knows — the running action and the last payout.
 */
export type IconComponent = Component<{ size?: number | string }>;

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
	/** Skill level that opens the action, when it is level-gated. */
	lockedAt?: number;
	inputs?: ActionInput[];
}

export interface InventoryItem {
	id: PlayableItemId;
	name: string;
	glyph: IconComponent;
	rarity: Rarity;
	count: number;
	kind: ItemKind;
}

export interface RunningAction {
	skill: PlayableSkillId;
	action: ActivityName;
	ms: number;
	xp: number;
}

/** One payout, as the difference it made. `key` only exists to remount the float. */
export interface BoardReward {
	action: ActivityId;
	key: number;
	xp: number;
	items: { itemId: ItemId; delta: number }[];
}
