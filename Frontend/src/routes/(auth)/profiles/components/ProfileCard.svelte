<script lang="ts">
	import ChevronsUp from '@lucide/svelte/icons/chevrons-up';
	import Coins from '@lucide/svelte/icons/coins';
	import Play from '@lucide/svelte/icons/play';
	import Timer from '@lucide/svelte/icons/timer';
	import Trash2 from '@lucide/svelte/icons/trash-2';
	import Meter from '$lib/components/core/Meter.svelte';
	import StatPill from '$lib/components/game/StatPill.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import * as Card from '$lib/components/ui/card';
	import type { Profile } from '../data';

	/*
	 * One save-slot panel from the design system's profiles template: identity
	 * row with avatar tile and Active badge, stat pills, an inset skill-meter
	 * well, then Resume/Load and Delete. Actions are static — purely
	 * presentational until the game board hooks them up.
	 */
	interface Props {
		profile: Profile;
	}

	let { profile }: Props = $props();
</script>

<Card.Root>
	<Row class="items-center gap-(--sp-5) px-(--card-spacing)">
		<Row
			class="size-11 shrink-0 items-center justify-center rounded-md border border-verdant-400/25 bg-verdant-400/10 text-text-accent"
		>
			<profile.icon size={22} />
		</Row>
		<div class="grid min-w-0 gap-(--sp-1)">
			<p class="oi-display-sm truncate text-text-strong">{profile.name}</p>
			<p class="oi-body-sm text-text-muted">Last played {profile.lastPlayed}</p>
		</div>
		{#if profile.active}
			<Badge variant="accent" class="ml-auto">Active</Badge>
		{/if}
	</Row>

	<Row class="flex-wrap gap-(--gap-stack) px-(--card-spacing)">
		<StatPill icon={ChevronsUp} label="Total level" value={profile.totalLevel} tone="xp" />
		<StatPill icon={Coins} label="Gold" value={profile.gold} />
		<StatPill icon={Timer} label="Playtime" value={profile.playtime} />
	</Row>

	<div
		class="mx-(--card-spacing) grid gap-(--gap-stack) rounded-md bg-surface-inset px-(--sp-5) py-(--pad-card) shadow-(--inset-well)"
	>
		{#each profile.skills as skill (skill.name)}
			<div class="grid grid-cols-[110px_1fr] items-center gap-(--gap-grid)">
				<Row class="oi-body-sm items-center gap-(--sp-3) text-text-body">
					<skill.icon size={13} />
					{skill.name}
				</Row>
				<Meter value={skill.pct} tone="skill" size="sm" label="{skill.name} progress" />
			</div>
		{/each}
	</div>

	<Card.Footer class="mt-(--sp-1) flex-wrap gap-(--gap-stack)">
		<Button variant={profile.active ? 'primary' : 'secondary'}>
			<Play />
			{profile.active ? 'Resume' : 'Load'}
		</Button>
		<Button variant="danger" class="ml-auto">
			<Trash2 />
			Delete
		</Button>
	</Card.Footer>
</Card.Root>
