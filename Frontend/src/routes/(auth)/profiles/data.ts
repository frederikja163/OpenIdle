import Fish from '@lucide/svelte/icons/fish';
import Hammer from '@lucide/svelte/icons/hammer';
import Pickaxe from '@lucide/svelte/icons/pickaxe';
import Trees from '@lucide/svelte/icons/trees';
import type { Component } from 'svelte';
import type { ProfileDto } from '$lib/ws/protocol';

export type IconComponent = Component<{ size?: number | string }>;

export interface Skill {
	name: string;
	icon: IconComponent;
	pct: number;
}

export interface Profile {
	profileId: string;
	name: string;
	icon: IconComponent;
	lastPlayed: string;
	/** Selected on this connection — client-side knowledge the socket never reports. */
	active: boolean;
	totalLevel: number;
	gold: string;
	playtime: string;
	skills: Skill[];
}

/*
 * TODO: every field below is fabricated. ProfileDto carries only Name and
 * ProfileId, so the icon, stats and skill meters are filler kept verbatim from
 * the design system's profiles template (Profiles.dc.html) to hold the layout.
 * Replace each field as the DTO grows one.
 */
const fillerStats: Omit<Profile, 'profileId' | 'name' | 'active'>[] = [
	{
		icon: Pickaxe,
		lastPlayed: '2 minutes ago',
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

/** Dresses a server profile in the card's view model, cycling the filler above. */
export function toProfile(dto: ProfileDto, index: number, active: boolean): Profile {
	return {
		profileId: dto.profileId,
		name: dto.name,
		...fillerStats[index % fillerStats.length],
		active
	};
}
