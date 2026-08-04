import Fish from '@lucide/svelte/icons/fish';
import Hammer from '@lucide/svelte/icons/hammer';
import Pickaxe from '@lucide/svelte/icons/pickaxe';
import Trees from '@lucide/svelte/icons/trees';
import type { Component } from 'svelte';

/*
 * Mock save-slot data, verbatim from the design system's profiles template
 * (Profiles.dc.html). The page is static for now: buttons render but carry no
 * handlers, and gold/playtime stay preformatted strings.
 */
export type IconComponent = Component<{ size?: number | string }>;

export interface Skill {
	name: string;
	icon: IconComponent;
	pct: number;
}

export interface Profile {
	name: string;
	icon: IconComponent;
	lastPlayed: string;
	active?: boolean;
	totalLevel: number;
	gold: string;
	playtime: string;
	skills: Skill[];
}

export const slotCapacity = 4;

export const profiles: Profile[] = [
	{
		name: 'Thorin',
		icon: Pickaxe,
		lastPlayed: '2 minutes ago',
		active: true,
		totalLevel: 13,
		gold: '1,284',
		playtime: '6h 12m',
		skills: [
			{ name: 'Mining', icon: Pickaxe, pct: 64 },
			{ name: 'Lumberjacking', icon: Trees, pct: 22 },
			{ name: 'Fishing', icon: Fish, pct: 51 },
			{ name: 'Crafting', icon: Hammer, pct: 8 }
		]
	},
	{
		name: 'Willow',
		icon: Trees,
		lastPlayed: '3 days ago',
		totalLevel: 27,
		gold: '4,913',
		playtime: '19h 40m',
		skills: [
			{ name: 'Mining', icon: Pickaxe, pct: 12 },
			{ name: 'Lumberjacking', icon: Trees, pct: 78 },
			{ name: 'Fishing', icon: Fish, pct: 35 },
			{ name: 'Crafting', icon: Hammer, pct: 41 }
		]
	},
	{
		name: 'Pike',
		icon: Fish,
		lastPlayed: '2 weeks ago',
		totalLevel: 5,
		gold: '97',
		playtime: '1h 03m',
		skills: [
			{ name: 'Mining', icon: Pickaxe, pct: 5 },
			{ name: 'Lumberjacking', icon: Trees, pct: 0 },
			{ name: 'Fishing', icon: Fish, pct: 44 },
			{ name: 'Crafting', icon: Hammer, pct: 0 }
		]
	}
];
