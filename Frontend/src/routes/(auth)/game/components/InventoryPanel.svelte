<script lang="ts">
	import ArrowDownUp from '@lucide/svelte/icons/arrow-down-up';
	import Gem from '@lucide/svelte/icons/gem';
	import Hammer from '@lucide/svelte/icons/hammer';
	import PackageOpen from '@lucide/svelte/icons/package-open';
	import Search from '@lucide/svelte/icons/search';
	import IconButton from '$lib/components/core/IconButton.svelte';
	import EmptyState from '$lib/components/feedback/EmptyState.svelte';
	import Tooltip from '$lib/components/feedback/Tooltip.svelte';
	import ResourceSlot from '$lib/components/game/ResourceSlot.svelte';
	import Panel from '$lib/components/layout/Panel.svelte';
	import TabStrip, { type Tab } from '$lib/components/layout/TabStrip.svelte';
	import { flavour, type InventoryItem } from '../data';

	/*
	 * The board's inventory: a tabbed well of item slots. The well is clamped and
	 * scrolls internally so a full pack never pushes the skills panel off-screen —
	 * the game board never scrolls the document.
	 */
	interface Props {
		items: InventoryItem[];
	}

	let { items }: Props = $props();

	let tab = $state('all');

	const tabs: Tab[] = $derived([
		{ id: 'all', label: 'All', count: items.length },
		{ id: 'res', label: 'Resources', icon: Gem },
		{ id: 'tools', label: 'Tools', icon: Hammer }
	]);

	const shown = $derived(
		items.filter((item) =>
			tab === 'all' ? true : tab === 'res' ? item.kind === 'res' : item.kind === 'tool'
		)
	);
</script>

<Panel title="Inventory" padded={false} class="min-h-[218px] shrink-0">
	{#snippet actions()}
		<!-- Both are inert until there is a real pack to sort or search. -->
		<IconButton icon={ArrowDownUp} label="Sort" size="sm" />
		<IconButton icon={Search} label="Search" size="sm" />
	{/snippet}

	<div class="px-(--gutter-panel)">
		<TabStrip {tabs} value={tab} onChange={(id) => (tab = id)} />
	</div>

	<div
		class="oi-scroll m-(--gutter-panel) max-h-37 min-h-27 overflow-y-auto rounded-md bg-surface-inset p-(--sp-5) shadow-(--inset-well)"
	>
		{#if shown.length === 0}
			<EmptyState
				compact
				icon={PackageOpen}
				title="No tools yet"
				hint="Level Crafting to 5 to forge your first axe."
			/>
		{:else}
			<div
				class="grid grid-cols-[repeat(auto-fill,var(--size-slot))] content-start gap-(--gap-grid)"
			>
				{#each shown as item (item.id)}
					<Tooltip
						title={item.name}
						meta={flavour[item.id]}
						rows={[
							{ label: 'Held', value: item.count },
							{ label: 'Rarity', value: item.rarity }
						]}
						side="bottom"
					>
						<ResourceSlot
							name={item.name}
							glyph={item.glyph}
							rarity={item.rarity}
							count={item.count}
						/>
					</Tooltip>
				{/each}
			</div>
		{/if}
	</div>
</Panel>
