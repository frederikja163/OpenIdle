import Axe from '@lucide/svelte/icons/axe';
import Box from '@lucide/svelte/icons/box';
import Diamond from '@lucide/svelte/icons/diamond';
import Fish from '@lucide/svelte/icons/fish';
import FishSymbol from '@lucide/svelte/icons/fish-symbol';
import Flame from '@lucide/svelte/icons/flame';
import Gem from '@lucide/svelte/icons/gem';
import Hammer from '@lucide/svelte/icons/hammer';
import Hexagon from '@lucide/svelte/icons/hexagon';
import Leaf from '@lucide/svelte/icons/leaf';
import Minus from '@lucide/svelte/icons/minus';
import Mountain from '@lucide/svelte/icons/mountain';
import Pickaxe from '@lucide/svelte/icons/pickaxe';
import Shrub from '@lucide/svelte/icons/shrub';
import Sparkles from '@lucide/svelte/icons/sparkles';
import TreePine from '@lucide/svelte/icons/tree-pine';
import Trees from '@lucide/svelte/icons/trees';
import Waves from '@lucide/svelte/icons/waves';
import type { Component } from 'svelte';
import type { ActionLock } from '$lib/components/game/ActionCard.svelte';
import type { Rarity } from '$lib/components/game/ItemArt.svelte';

/*
 * Mock board data, ported from the design system's game kit (gameData.js). The
 * design keeps glyphs as Lucide *names*; this codebase imports each icon and
 * passes the component, as the profiles page already does, so a typo fails the
 * build instead of rendering nothing.
 *
 * The backend has no game messages yet. When it grows them, this module is what
 * gets replaced — nothing in the components or the page markup knows it is mock.
 */
export type IconComponent = Component<{ size?: number | string }>;

export type ItemKind = 'res' | 'tool';

export interface Skill {
	id: string;
	name: string;
	icon: IconComponent;
	level: number;
	xp: number;
	xpMax: number;
}

export interface ActionInput {
	id: string;
	name: string;
	glyph: IconComponent;
	rarity?: Rarity;
	qty: number;
}

export interface GameAction {
	id: string;
	name: string;
	glyph: IconComponent;
	rarity: Rarity;
	/** Time for one completion, in milliseconds. */
	ms: number;
	xp: number;
	/** How many of the item one completion yields. */
	qty: number;
	/** Skill level that opens the action, when it is level-gated. */
	lockedAt?: number;
	locked?: boolean;
	lockedBy?: ActionLock;
	inputs?: ActionInput[];
}

export interface InventoryItem {
	id: string;
	name: string;
	glyph: IconComponent;
	rarity: Rarity;
	count: number;
	kind: ItemKind;
}

export const slotCapacity = 40;

export const skills: Skill[] = [
	{ id: 'mining', name: 'Mining', icon: Pickaxe, level: 7, xp: 64, xpMax: 100 },
	{ id: 'lumber', name: 'Lumberjacking', icon: Trees, level: 3, xp: 22, xpMax: 100 },
	{ id: 'fishing', name: 'Fishing', icon: Fish, level: 2, xp: 51, xpMax: 100 },
	{ id: 'crafting', name: 'Crafting', icon: Hammer, level: 1, xp: 8, xpMax: 100 },
	{ id: 'smithing', name: 'Smithing', icon: Flame, level: 0, xp: 0, xpMax: 100 }
];

export const actions: Record<string, GameAction[]> = {
	mining: [
		{
			id: 'talc',
			name: 'Mine Talc Ore',
			glyph: Mountain,
			rarity: 'common',
			ms: 5000,
			xp: 1,
			qty: 1
		},
		{
			id: 'gypsum',
			name: 'Mine Gypsum Ore',
			glyph: Box,
			rarity: 'common',
			ms: 5000,
			xp: 2,
			qty: 1
		},
		{
			id: 'calcite',
			name: 'Mine Calcite Ore',
			glyph: Gem,
			rarity: 'uncommon',
			ms: 7000,
			xp: 3,
			qty: 1
		},
		{
			id: 'fluorite',
			name: 'Mine Fluorite Ore',
			glyph: Diamond,
			rarity: 'rare',
			ms: 8000,
			xp: 4,
			qty: 1
		},
		{
			id: 'apatite',
			name: 'Mine Apatite Ore',
			glyph: Hexagon,
			rarity: 'rare',
			ms: 9000,
			xp: 5,
			qty: 1,
			lockedAt: 12
		}
	],
	lumber: [
		{ id: 'birch', name: 'Fell Birch', glyph: Trees, rarity: 'common', ms: 4000, xp: 1, qty: 2 },
		{ id: 'oak', name: 'Fell Oak', glyph: TreePine, rarity: 'common', ms: 6000, xp: 3, qty: 1 },
		{
			id: 'yew',
			name: 'Fell Yew',
			glyph: Leaf,
			rarity: 'uncommon',
			ms: 9000,
			xp: 6,
			qty: 1,
			lockedAt: 8
		},
		{
			id: 'ironwood',
			name: 'Fell Ironwood',
			glyph: Shrub,
			rarity: 'epic',
			ms: 14000,
			xp: 12,
			qty: 1,
			lockedAt: 20
		}
	],
	fishing: [
		{ id: 'shrimp', name: 'Net Shrimp', glyph: Fish, rarity: 'common', ms: 4000, xp: 1, qty: 2 },
		{
			id: 'trout',
			name: 'Catch Trout',
			glyph: FishSymbol,
			rarity: 'common',
			ms: 6000,
			xp: 3,
			qty: 1
		},
		{
			id: 'eel',
			name: 'Catch Eel',
			glyph: Waves,
			rarity: 'uncommon',
			ms: 10000,
			xp: 7,
			qty: 1,
			lockedAt: 9
		}
	],
	crafting: [
		{
			id: 'talcpick',
			name: 'Craft Talc Pickaxe Head',
			glyph: Pickaxe,
			rarity: 'common',
			ms: 5000,
			xp: 1,
			qty: 1,
			inputs: [{ id: 'talc', name: 'Talc Ore', glyph: Mountain, qty: 3 }]
		},
		{
			id: 'talcaxe',
			name: 'Craft Talc Axe Head',
			glyph: Axe,
			rarity: 'common',
			ms: 5000,
			xp: 1,
			qty: 1,
			inputs: [{ id: 'talc', name: 'Talc Ore', glyph: Mountain, qty: 3 }]
		},
		{
			id: 'balsa',
			name: 'Craft Balsa Handle',
			glyph: Minus,
			rarity: 'common',
			ms: 5000,
			xp: 1,
			qty: 2,
			inputs: [{ id: 'birch', name: 'Birch Log', glyph: Trees, qty: 2 }]
		},
		{
			id: 'calcitepick',
			name: 'Craft Calcite Pickaxe Head',
			glyph: Pickaxe,
			rarity: 'uncommon',
			ms: 5000,
			xp: 3,
			qty: 1,
			inputs: [{ id: 'calcite', name: 'Calcite Ore', glyph: Gem, rarity: 'uncommon', qty: 6 }]
		},
		{
			id: 'talcpickaxe',
			name: 'Assemble Talc Pickaxe',
			glyph: Pickaxe,
			rarity: 'uncommon',
			ms: 8000,
			xp: 6,
			qty: 1,
			inputs: [
				{ id: 'talcpick', name: 'Talc Pickaxe Head', glyph: Pickaxe, qty: 1 },
				{ id: 'balsa', name: 'Balsa Handle', glyph: Minus, qty: 1 }
			]
		},
		{
			id: 'gypsumaxe',
			name: 'Craft Gypsum Axe Head',
			glyph: Axe,
			rarity: 'common',
			ms: 5000,
			xp: 2,
			qty: 1,
			lockedAt: 10,
			lockedBy: 'level',
			inputs: [{ id: 'gypsum', name: 'Gypsum Ore', glyph: Box, qty: 4 }]
		},
		{
			id: 'fluoritehammer',
			name: 'Craft Fluorite Hammer Head',
			glyph: Hammer,
			rarity: 'rare',
			ms: 5000,
			xp: 4,
			qty: 1,
			locked: true,
			lockedBy: 'items',
			inputs: [{ id: 'fluorite', name: 'Fluorite Ore', glyph: Diamond, rarity: 'rare', qty: 5 }]
		}
	],
	smithing: []
};

export const inventory: InventoryItem[] = [
	{ id: 'talc', name: 'Talc Ore', glyph: Mountain, rarity: 'common', count: 18, kind: 'res' },
	{ id: 'gypsum', name: 'Gypsum Ore', glyph: Box, rarity: 'common', count: 9, kind: 'res' },
	{ id: 'calcite', name: 'Calcite Ore', glyph: Gem, rarity: 'uncommon', count: 4, kind: 'res' },
	{ id: 'fluorite', name: 'Fluorite Ore', glyph: Diamond, rarity: 'rare', count: 1, kind: 'res' },
	{ id: 'birch', name: 'Birch Log', glyph: Trees, rarity: 'common', count: 26, kind: 'res' },
	{ id: 'oak', name: 'Oak Log', glyph: TreePine, rarity: 'common', count: 5, kind: 'res' },
	{ id: 'shrimp', name: 'Shrimp', glyph: Fish, rarity: 'common', count: 12, kind: 'res' },
	{
		id: 'talcpick',
		name: 'Talc Pickaxe Head',
		glyph: Pickaxe,
		rarity: 'common',
		count: 2,
		kind: 'tool'
	},
	{ id: 'balsa', name: 'Balsa Handle', glyph: Minus, rarity: 'common', count: 1, kind: 'tool' }
];

/** Item descriptions, shown in the inventory tooltip. Not every item has one. */
export const flavour: Record<string, string> = {
	talc: 'Softest known mineral. Crumbles in the hand.',
	calcite: 'Rings faintly when struck. Traders like that.',
	fluorite: 'Glows under lamplight. Worth the extra swings.'
};
