<script lang="ts">
	import { cn, type WithElementRef } from '$lib/utils/stylingUtils.js';
	import type { Snippet } from 'svelte';
	import type { HTMLAttributes } from 'svelte/elements';

	/*
	 * OpenIdle Design System panel (Panel.jsx): --surface-panel over a faint
	 * top sheen, --line-soft border, 12px radius, --shadow-panel, overflow
	 * hidden. Vega's --card-spacing is pinned to the DS --gutter-panel (16px)
	 * so panel padding and row gutters are one token.
	 */
	interface Props extends WithElementRef<HTMLAttributes<HTMLDivElement>> {
		children?: Snippet;
	}

	let { ref = $bindable(null), class: className, children, ...restProps }: Props = $props();
</script>

<div
	bind:this={ref}
	data-slot="card"
	class={cn(
		'group/card flex flex-col gap-(--sp-5) overflow-hidden rounded-lg border border-line-soft bg-surface-panel bg-(image:--bg-panel-sheen) py-(--card-spacing) text-text-body shadow-(--shadow-panel) [--card-spacing:var(--gutter-panel)]',
		className
	)}
	{...restProps}
>
	{@render children?.()}
</div>
