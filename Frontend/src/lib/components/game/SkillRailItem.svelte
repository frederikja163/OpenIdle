<script lang="ts">
	import type { Component } from 'svelte';
	import Meter from '$lib/components/core/Meter.svelte';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System skill rail entry (SkillRailItem.jsx): the glyph tile,
	 * name and XP meter for one skill. Collapsed, it keeps only the tile — the
	 * running dot has to survive that, because it is how a visitor tracks which
	 * skill is ticking while looking at another one.
	 */
	interface Props {
		name: string;
		icon: Component<{ size?: number | string }>;
		level?: number;
		xp?: number;
		xpMax?: number;
		active?: boolean;
		running?: boolean;
		collapsed?: boolean;
		onclick?: () => void;
	}

	let {
		name,
		icon: SkillIcon,
		level = 0,
		xp = 0,
		xpMax = 100,
		active = false,
		running = false,
		collapsed = false,
		onclick
	}: Props = $props();
</script>

<button
	type="button"
	{onclick}
	title={collapsed ? `${name} · Level ${level}` : undefined}
	class={cn(
		'grid w-full cursor-pointer gap-(--sp-3) rounded-sm border text-left transition-[background-color] duration-(--dur-fast) ease-out focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring',
		collapsed ? 'justify-items-center p-(--sp-4)' : 'justify-items-stretch px-(--sp-5) py-(--sp-4)',
		active ? 'border-line-accent bg-surface-active' : 'border-transparent hover:bg-action-quiet'
	)}
>
	<span class="flex w-full items-center gap-(--sp-4)">
		<span
			class={cn(
				'relative inline-flex size-6.5 items-center justify-center rounded-xs bg-surface-inset shadow-(--inset-well)',
				active ? 'text-verdant-300' : 'text-(--ink-400)'
			)}
		>
			<SkillIcon size={15} />
			{#if running}
				<span
					class="absolute -top-0.75 -right-0.75 size-1.75 animate-[oi-pulse_1.6s_var(--ease-in-out)_infinite] rounded-(--radius-full) bg-verdant-400 shadow-(--glow-accent)"
				></span>
			{/if}
		</span>
		{#if !collapsed}
			<span class="grid min-w-0 gap-px">
				<!--
					13px display is the one size the type scale has no token for — the
					design spells it out here too, because --type-display-sm at 15px
					wraps "Lumberjacking" onto three lines in a 176px rail.
				-->
				<span
					class={cn(
						'font-display text-[13px]/[1.2] font-semibold tracking-(--track-display) wrap-anywhere',
						active ? 'text-text-accent' : 'text-text-body'
					)}
				>
					{name}
				</span>
				<span class="oi-num-sm text-text-faint">Lv {level}</span>
			</span>
		{/if}
	</span>
	{#if !collapsed}
		<Meter value={xp} max={xpMax} tone="xp" size="sm" label="{name} experience" />
	{/if}
</button>
