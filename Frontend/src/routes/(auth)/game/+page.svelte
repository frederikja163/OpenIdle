<script lang="ts">
	import StatPill from '$lib/components/game/StatPill.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import ChevronsUp from '@lucide/svelte/icons/chevrons-up';
	import Coins from '@lucide/svelte/icons/coins';
	import Package from '@lucide/svelte/icons/package';
	import Zap from '@lucide/svelte/icons/zap';
	import InventoryPanel from './components/InventoryPanel.svelte';
	import SkillsPanel from './components/SkillsPanel.svelte';
	import { BoardState } from './state.svelte';

	/*
	 * The game board, per the design system's GameBoard.jsx: HUD stat strip over
	 * the inventory over the skills. The idle loop is simulated client-side — see
	 * ./state.svelte.ts — because the protocol carries no game messages yet.
	 */
	const board = new BoardState();

	// Re-running whenever `running` changes is the point: switching or stopping an
	// action tears the old interval down before the next one is armed.
	$effect(() => board.run());
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

	<Row class="flex-wrap items-center gap-(--sp-4)">
		<StatPill icon={ChevronsUp} label="Total level" value={board.totalLevel} tone="xp" />
	</Row>

	<InventoryPanel items={board.inventory} />

	<SkillsPanel
		skills={board.skills}
		actions={board.actions}
		activeSkill={board.activeSkill}
		running={board.running}
		progress={board.progress}
		reward={board.reward}
		held={board.held}
		onSelectSkill={(id) => (board.activeSkill = id)}
		onStartAction={(action) => board.start(action)}
		onStopAction={() => board.stop()}
	/>
</div>
