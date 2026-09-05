<script lang="ts">
	import { resolve } from '$app/paths';
	import ChevronsUp from '@lucide/svelte/icons/chevrons-up';
	import Users from '@lucide/svelte/icons/users';
	import EmptyState from '$lib/components/feedback/EmptyState.svelte';
	import StatPill from '$lib/components/game/StatPill.svelte';
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { gameState, loadGame } from '$lib/state/game.svelte';
	import { profilesState } from '$lib/state/profiles.svelte';
	import { sessionIntent } from '$lib/state/session.svelte';
	import InventoryPanel from './components/InventoryPanel.svelte';
	import SkillsPanel from './components/SkillsPanel.svelte';
	import { BoardState } from './state.svelte';

	/*
	 * The game board, per the design system's GameBoard.jsx: HUD stat strip over
	 * the inventory over the skills. The board reads the game store; see
	 * ./state.svelte.ts for how the store's totals become the panels' props.
	 */
	const board = new BoardState();

	// Fires on arrival and again after a reconnect: the session reset puts the
	// store back to 'idle', and the profile replay restores the selection.
	$effect(() => {
		if (profilesState.selectedProfileId !== null && gameState.status === 'idle') {
			void loadGame();
		}
	});

	// Re-running whenever the running activity changes is the point: stopping
	// tears the clock down, and a payout re-arms it against the new start.
	$effect(() => board.run());

	// No profile on this connection and none being restored. The intent is not
	// reactive, so the refusal a replay leaves in selectError is what re-runs
	// this when a remembered profile turns out to be gone.
	const noProfile = $derived(
		profilesState.selectedProfileId === null &&
			(sessionIntent.profileId === null || profilesState.selectError !== null)
	);
</script>

<!--
	The topbar is a fixed 52px band owned by the (auth) layout, so the board takes
	exactly the rest of the viewport and clips. Panels scroll internally from
	there — the game never scrolls the document.
-->
<div
	class="oi-board flex h-[calc(100dvh-var(--h-topbar))] flex-col gap-(--gutter-app) overflow-hidden p-(--gutter-app)"
>
	<!--
		The design gives the board no visible title — the HUD strip is the header,
		and every pixel above it belongs to the game. The panels below are still h2s
		though, so the document needs the level above them to exist somewhere.
	-->
	<h1 class="sr-only">Game board</h1>

	{#if noProfile}
		<Column class="m-auto items-center gap-(--sp-6)">
			<EmptyState
				icon={Users}
				title="No profile selected"
				hint="The board plays whichever profile you load."
			/>
			{#if profilesState.selectError}
				<p role="alert" class="oi-body-md text-text-danger">{profilesState.selectError}</p>
			{/if}
			<Button variant="primary" href={resolve('/profiles')}>Choose a profile</Button>
		</Column>
	{:else if profilesState.selectedProfileId === null}
		<p class="oi-body-md text-text-muted">Restoring your profile…</p>
	{:else}
		<Row class="flex-wrap items-center gap-(--sp-4)">
			<StatPill icon={ChevronsUp} label="Total level" value={board.totalLevel} tone="xp" />
			{#if gameState.status === 'loading'}
				<span class="oi-body-md text-text-faint">Loading…</span>
			{/if}
			{#if gameState.error}
				<p role="alert" class="oi-body-md text-text-danger">{gameState.error}</p>
				<Button size="sm" onclick={() => void loadGame()}>Retry</Button>
			{/if}
			{#if gameState.actionError}
				<p role="alert" class="oi-body-md text-text-danger">{gameState.actionError}</p>
			{/if}
		</Row>

		<InventoryPanel items={board.inventory} />

		<SkillsPanel
			skills={board.skills}
			actions={board.actions}
			activeSkill={board.selectedSkill}
			running={board.running}
			progress={board.progress}
			reward={board.reward}
			held={board.held}
			onSelectSkill={(id) => (board.activeSkill = id)}
			onStartAction={(action) => board.start(action)}
			onStopAction={() => board.stop()}
		/>
	{/if}
</div>
