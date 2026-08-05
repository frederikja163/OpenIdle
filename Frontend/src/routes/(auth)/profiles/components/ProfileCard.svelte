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
	import * as Dialog from '$lib/components/ui/dialog';
	import type { Profile } from '../data';

	/*
	 * One save-slot panel from the design system's profiles template: identity
	 * row with avatar tile and Active badge, stat pills, an inset skill-meter
	 * well, then Resume/Load and Delete. Selecting is raised to the page, which
	 * owns the navigation that follows it; Delete asks for confirmation but has
	 * no backend message behind it yet.
	 */
	interface Props {
		profile: Profile;
		onSelect: () => void;
		/** This card's select is in flight. */
		selecting?: boolean;
		/** Some card's select is in flight — only one may run at a time. */
		disabled?: boolean;
	}

	let { profile, onSelect, selecting = false, disabled = false }: Props = $props();
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
		<Button
			variant={profile.active ? 'primary' : 'secondary'}
			{disabled}
			aria-busy={selecting}
			onclick={onSelect}
		>
			<Play />
			{selecting ? 'Loading…' : profile.active ? 'Resume' : 'Load'}
		</Button>
		<!--
			Dialog.Root renders no element of its own, so the trigger Button is still
			a direct flex child of the footer and keeps pushing itself right.
		-->
		<Dialog.Root>
			<Dialog.Trigger>
				{#snippet child({ props })}
					<Button variant="danger" class="ml-auto" {...props}>
						<Trash2 />
						Delete
					</Button>
				{/snippet}
			</Dialog.Trigger>
			<!-- No corner X: with only Cancel and Delete it duplicates Cancel. -->
			<Dialog.Content showCloseButton={false}>
				<Dialog.Header>
					<Dialog.Title>Delete {profile.name}?</Dialog.Title>
					<Dialog.Description>
						This deletes the profile and everything on it. It cannot be undone.
					</Dialog.Description>
				</Dialog.Header>
				<Dialog.Footer>
					<!--
						Cancel comes first so the focus trap lands on it rather than on the
						destructive button. Reordering these moves the initial focus.
					-->
					<Dialog.Close>
						{#snippet child({ props })}
							<Button variant="ghost" {...props}>Cancel</Button>
						{/snippet}
					</Dialog.Close>
					<!--
						TODO: confirming only dismisses. There is no delete message on the
						wire or in the backend yet; when one lands, call it here and clear
						profilesState.selectedProfileId if this profile was the selected one.
					-->
					<Dialog.Close>
						{#snippet child({ props })}
							<Button variant="danger" {...props}>
								<Trash2 />
								Delete
							</Button>
						{/snippet}
					</Dialog.Close>
				</Dialog.Footer>
			</Dialog.Content>
		</Dialog.Root>
	</Card.Footer>
</Card.Root>
