<script lang="ts">
	import ArrowDownUp from '@lucide/svelte/icons/arrow-down-up';
	import Gem from '@lucide/svelte/icons/gem';
	import Hammer from '@lucide/svelte/icons/hammer';
	import PackageOpen from '@lucide/svelte/icons/package-open';
	import Search from '@lucide/svelte/icons/search';
	import SearchX from '@lucide/svelte/icons/search-x';
	import IconButton from '$lib/components/core/IconButton.svelte';
	import EmptyState from '$lib/components/feedback/EmptyState.svelte';
	import Tooltip from '$lib/components/feedback/Tooltip.svelte';
	import ResourceSlot from '$lib/components/game/ResourceSlot.svelte';
	import Panel from '$lib/components/layout/Panel.svelte';
	import TabStrip, { type Tab } from '$lib/components/layout/TabStrip.svelte';
	import { Input } from '$lib/components/ui/input';
	import type { InventoryItem, ItemKind } from '$lib/game/types';

	/*
	 * The board's inventory: a tabbed well of item slots. The well is clamped and
	 * scrolls internally so a full pack never pushes the skills panel off-screen —
	 * the game board never scrolls the document.
	 */
	interface Props {
		items: InventoryItem[];
	}

	let { items }: Props = $props();

	/** The tabs are the item kinds plus an "everything" one, so the ids are too. */
	type InventoryTab = 'all' | ItemKind;

	let tab = $state<InventoryTab>('all');
	let byCount = $state(false);
	let searching = $state(false);
	let query = $state('');

	const tabs: Tab<InventoryTab>[] = $derived([
		{ id: 'all', label: 'All', count: items.length },
		{ id: 'res', label: 'Resources', icon: Gem },
		{ id: 'tool', label: 'Tools', icon: Hammer }
	]);

	const EMPTY_COPY: Record<InventoryTab, { title: string; hint: string }> = {
		all: { title: 'Your pack is empty', hint: 'Start an action to gather your first resources.' },
		res: { title: 'No resources yet', hint: 'Mine ore or chop logs to fill this tab.' },
		tool: { title: 'No tools yet', hint: 'Craft handles and heads from ore and logs.' }
	};

	const needle = $derived(searching ? query.trim().toLowerCase() : '');

	const matching = $derived(
		items.filter(
			(item) =>
				(tab === 'all' || item.kind === tab) &&
				(needle === '' || item.name.toLowerCase().includes(needle))
		)
	);

	// Sorting copies rather than ordering in place: `items` is the board's own
	// derived array. Array.sort is stable, so ties keep catalog order.
	const shown = $derived(byCount ? [...matching].sort((a, b) => b.count - a.count) : matching);

	function toggleSearch(): void {
		searching = !searching;
		if (!searching) {
			query = '';
		}
	}

	// A field the visitor just asked for should not need a second click. Driven
	// off the bound element rather than `autofocus`, which would also take focus
	// on a plain page load, and than a `use:` action, which components do not take.
	let field = $state<HTMLInputElement | null>(null);
	$effect(() => {
		field?.focus();
	});
</script>

<Panel title="Inventory" padded={false} class="min-h-[218px] shrink-0">
	{#snippet actions()}
		<IconButton
			icon={ArrowDownUp}
			label="Sort by count"
			size="sm"
			active={byCount}
			aria-pressed={byCount}
			onclick={() => (byCount = !byCount)}
		/>
		<IconButton
			icon={Search}
			label="Search"
			size="sm"
			active={searching}
			aria-expanded={searching}
			onclick={toggleSearch}
		/>
	{/snippet}

	<div class="px-(--gutter-panel)">
		<TabStrip {tabs} value={tab} onChange={(id) => (tab = id)} />
	</div>

	{#if searching}
		<div class="px-(--gutter-panel) pt-(--sp-4)">
			<Input
				type="search"
				bind:ref={field}
				bind:value={query}
				placeholder="Search items"
				aria-label="Search items"
			/>
		</div>
	{/if}

	<div
		class="oi-scroll m-(--gutter-panel) max-h-37 min-h-27 overflow-y-auto rounded-md bg-surface-inset p-(--sp-5) shadow-(--inset-well)"
	>
		{#if shown.length === 0}
			{#if needle === ''}
				<EmptyState
					compact
					icon={PackageOpen}
					title={EMPTY_COPY[tab].title}
					hint={EMPTY_COPY[tab].hint}
				/>
			{:else}
				<EmptyState
					compact
					icon={SearchX}
					title="No items match"
					hint="Nothing in this tab is named like that."
				/>
			{/if}
		{:else}
			<div
				class="grid grid-cols-[repeat(auto-fill,var(--size-slot))] content-start gap-(--gap-grid)"
			>
				{#each shown as item (item.id)}
					<Tooltip
						title={item.name}
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
