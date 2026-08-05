<script lang="ts">
	import type { Component } from 'svelte';
	import ItemArt, { type Rarity } from '$lib/components/game/ItemArt.svelte';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System inventory slot (ResourceSlot.jsx): a fixed
	 * --size-slot tile holding the item's art with its held count tucked into the
	 * bottom-right corner over a scrim, so a long count never widens the grid.
	 *
	 * The design also puts the name in a `title`, which on the board would fire a
	 * native tooltip underneath the styled one the panel already wraps this in.
	 * It is an aria-label here instead: same text, no second popup, and it beats
	 * the count out of the accessible name, which is otherwise the only content.
	 */
	interface Props {
		name: string;
		glyph: Component<{ size?: number | string }>;
		rarity?: Rarity;
		count?: number;
		selected?: boolean;
		onclick?: () => void;
		class?: string;
	}

	let {
		name,
		glyph,
		rarity = 'common',
		count = 0,
		selected = false,
		onclick,
		class: className
	}: Props = $props();
</script>

<button
	type="button"
	{onclick}
	aria-label="{name} · {count}"
	class={cn(
		'relative size-(--size-slot) cursor-pointer rounded-sm p-0.75 transition-[background-color,box-shadow] duration-(--dur-fast) ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
		selected
			? 'border border-line-accent bg-surface-active shadow-(--shadow-card-hover)'
			: 'border border-line-soft bg-surface-card hover:bg-surface-card-hover hover:shadow-(--shadow-card-hover)',
		className
	)}
>
	<ItemArt {glyph} {rarity} size={44} width="100%" height="100%" variant="tinted" />
	<span
		class="oi-num-sm absolute right-0.5 bottom-px rounded-xs bg-[rgba(8,11,16,.82)] px-0.75 text-text-strong"
	>
		{count}
	</span>
</button>
