<script lang="ts">
	import CalendarPlus from '@lucide/svelte/icons/calendar-plus';
	import ChevronsUp from '@lucide/svelte/icons/chevrons-up';
	import Play from '@lucide/svelte/icons/play';
	import Trash2 from '@lucide/svelte/icons/trash-2';
	import UserRound from '@lucide/svelte/icons/user-round';
	import StatPill from '$lib/components/game/StatPill.svelte';
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import * as Card from '$lib/components/ui/card';
	import * as Dialog from '$lib/components/ui/dialog';
	import { formatDate, formatTimeAgo, formatWireName } from '$lib/utils/formatUtils';
	import { cn } from '$lib/utils/stylingUtils';
	import type { ProfileDto } from '$lib/ws/protocol';

	/*
	 * One save-slot panel from the design system's profiles template: identity row
	 * with avatar tile and status badge, stat pills, then Resume/Load and Delete.
	 * Selecting is raised to the page, which owns the navigation that follows it;
	 * Delete asks for confirmation but has no backend message behind it yet.
	 *
	 * The badge and the button deliberately read different facts. The badge is the
	 * server's: a profile is online when any connection anywhere has it selected.
	 * `selected` is this socket's, and the socket never reports it back.
	 */
	interface Props {
		profile: ProfileDto;
		/** Selected on this connection — client-side knowledge the socket never reports. */
		selected: boolean;
		onSelect: () => void;
		/** This card's select is in flight. */
		selecting?: boolean;
		/** Some card's select is in flight — only one may run at a time. */
		disabled?: boolean;
	}

	let { profile, selected, onSelect, selecting = false, disabled = false }: Props = $props();

	// The backend sends no lastActive at all while the profile is online, because
	// ToDto passes null and SocketJsonSerializer omits nulls rather than writing
	// them. So the absence is the whole signal; there is no null to also check.
	const online = $derived(profile.lastActive === undefined);
	const lastPlayed = $derived(formatTimeAgo(profile.lastActive));
	// Idle arrives spelled two ways: the column is nullable, and the generated
	// ActivityId carries a synthetic 'None' the contract never clears to.
	const activity = $derived(
		profile.activity && profile.activity !== 'None' ? formatWireName(profile.activity) : null
	);
	const created = $derived(formatDate(profile.creationTime));
</script>

<Card.Root>
	<Row class="items-center gap-(--sp-5) px-(--card-spacing)">
		<!--
			One neutral mark for every profile: nothing on the DTO distinguishes them,
			and an icon picked per card would be decoration dressed as data. The tone
			carries the real fact instead — colours.css reserves the accent for the
			running action and for selection, so an idle profile does not spend it.
		-->
		<Row
			class={cn(
				'size-11 shrink-0 items-center justify-center rounded-md border',
				online
					? 'border-verdant-400/25 bg-verdant-400/10 text-text-accent'
					: 'border-transparent bg-action-quiet text-text-faint'
			)}
		>
			<UserRound size={22} />
		</Row>
		<Column class="min-w-0 gap-(--sp-1)">
			<p class="oi-display-sm truncate text-text-strong">{profile.name}</p>
			<!--
				Nothing at all when the timestamp is the migration's backfill: the row
				predates the column, so "last played" is genuinely unknown rather than
				long ago, and it corrects itself the next time the profile connects.
			-->
			{#if online}
				<p class="oi-body-sm text-text-muted">Active now</p>
			{:else if lastPlayed}
				<p class="oi-body-sm text-text-muted">Last played {lastPlayed}</p>
			{/if}
		</Column>
		<!--
			Both badges are gated on being online, because an activity outlives the
			connection that started it: Profile.ActivityId is a column, and only
			ClearActivityAsync empties it, so a player who disconnects mid-activity
			leaves one set. Spending the accent — which colours.css reserves for the
			running action — on a profile that stopped running it would be a lie.
		-->
		{#if online && activity}
			<Badge variant="accent" class="ml-auto self-start">{activity}</Badge>
		{:else if online}
			<Badge variant="neutral" class="ml-auto self-start">Online</Badge>
		{/if}
	</Row>

	<Row class="flex-wrap gap-(--gap-stack) px-(--card-spacing)">
		<StatPill icon={ChevronsUp} label="Total level" value={profile.totalLevel} tone="xp" />
		{#if created}
			<StatPill icon={CalendarPlus} label="Created" value={created} />
		{/if}
	</Row>

	<!--
		mt-auto, not a fixed margin: Card.Root is a flex column, so when the grid
		stretches this card to the row height the surplus collects below the last
		child. Without it the buttons strand themselves mid-card.
	-->
	<Card.Footer class="mt-auto flex-wrap gap-(--gap-stack)">
		<Button
			variant={selected ? 'primary' : 'secondary'}
			{disabled}
			aria-busy={selecting}
			onclick={onSelect}
		>
			<Play />
			{selecting ? 'Loading…' : selected ? 'Resume' : 'Load'}
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
