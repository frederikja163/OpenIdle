<script lang="ts">
	import type { Component } from 'svelte';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System empty state (EmptyState.jsx): a centred glyph tile
	 * with a title and a hint naming what would fill the space. `compact` is for
	 * the inventory well, where the full padding would not fit.
	 */
	interface Props {
		icon: Component<{ size?: number | string }>;
		title: string;
		hint?: string;
		compact?: boolean;
		class?: string;
	}

	let { icon: StateIcon, title, hint, compact = false, class: className }: Props = $props();
</script>

<div
	class={cn(
		'grid justify-items-center gap-(--sp-4) text-center',
		compact ? 'p-(--sp-8)' : 'px-(--sp-8) py-(--sp-11)',
		className
	)}
>
	<span class="grid size-10 place-items-center rounded-md bg-action-quiet text-(--ink-500)">
		<StateIcon size={20} />
	</span>
	<span class="oi-display-sm text-text-muted">{title}</span>
	{#if hint}
		<span class="oi-body-sm max-w-70 text-pretty text-text-faint">{hint}</span>
	{/if}
</div>
