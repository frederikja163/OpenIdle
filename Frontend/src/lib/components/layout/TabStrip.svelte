<script lang="ts">
	import type { Component } from 'svelte';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System tab strip (TabStrip.jsx): a hairline-underlined row
	 * where the selected tab is marked by a verdant rule drawn as an inset shadow
	 * rather than a border, so switching tabs never shifts the strip's height.
	 */
	export interface Tab {
		id: string;
		label: string;
		icon?: Component<{ size?: number | string }>;
		count?: number;
	}

	interface Props {
		tabs: Tab[];
		value: string;
		onChange?: (id: string) => void;
		class?: string;
	}

	let { tabs, value, onChange, class: className }: Props = $props();
</script>

<div
	role="tablist"
	class={cn(
		'flex h-(--h-tabstrip) items-stretch gap-(--sp-1) border-b border-line-hairline',
		className
	)}
>
	{#each tabs as tab (tab.id)}
		{@const selected = tab.id === value}
		<button
			type="button"
			role="tab"
			aria-selected={selected}
			onclick={() => onChange?.(tab.id)}
			class={cn(
				'oi-label-md inline-flex cursor-pointer items-center gap-(--sp-3) px-(--sp-5) transition-[color,box-shadow] duration-(--dur-fast) ease-out focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring',
				selected
					? 'text-text-strong shadow-[inset_0_-2px_0_var(--verdant-400)]'
					: 'text-text-faint hover:text-text-body'
			)}
		>
			{#if tab.icon}
				<!-- The glyph lifts to full accent on the selected tab while the label
				     stays neutral, so the strip reads as one accent mark, not two. -->
				<span class={selected ? 'text-verdant-300' : undefined}>
					<tab.icon size={13} />
				</span>
			{/if}
			{tab.label}
			{#if tab.count != null}
				<span class="oi-num-sm text-text-faint">{tab.count}</span>
			{/if}
		</button>
	{/each}
</div>
