<script lang="ts">
	import type { Snippet } from 'svelte';
	import {
		autoUpdate,
		computePosition,
		flip,
		offset,
		shift,
		type Placement
	} from '@floating-ui/dom';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System tooltip (Tooltip.jsx): a hover card describing the
	 * wrapped control, with an optional table of stat rows.
	 *
	 * The design opens it on pointer events only. Focus is added here because
	 * everything this wraps on the board — resource slots, action cards — is a
	 * real button, so a keyboard visitor would otherwise never see the panel.
	 *
	 * The panel cannot live inside the wrapper's layout tree. Every board parent
	 * clips it — the inventory well, the skill grid and the board itself all
	 * scroll or hide overflow — so it is teleported to <body> and positioned as
	 * `fixed` by floating-ui, which also flips or shifts it when the viewport
	 * edge would otherwise cut it off. Teleporting to body keeps it above every
	 * stacking context and clip on the page.
	 */
	export type TooltipRowTone = 'neutral' | 'xp' | 'danger';

	export interface TooltipRow {
		label: string;
		value: string | number;
		tone?: TooltipRowTone;
	}

	type TooltipSide = 'top' | 'bottom' | 'right';

	interface Props {
		title: string;
		meta?: string;
		rows?: TooltipRow[];
		side?: TooltipSide;
		children?: Snippet;
	}

	let { title, meta, rows = [], side = 'top', children }: Props = $props();

	let open = $state(false);
	let anchor = $state<HTMLElement | null>(null);
	let panel = $state<HTMLElement | null>(null);
	let x = $state(0);
	let y = $state(0);

	const placements: Record<TooltipSide, Placement> = {
		top: 'top',
		bottom: 'bottom',
		right: 'right'
	};

	$effect(() => {
		if (!open || !anchor || !panel) return;
		const trigger = anchor;
		const tip = panel;
		const update = () =>
			computePosition(trigger, tip, {
				strategy: 'fixed',
				placement: placements[side],
				middleware: [offset(8), flip(), shift({ padding: 8 })]
			}).then(({ x: nextX, y: nextY }) => {
				x = nextX;
				y = nextY;
			});
		return autoUpdate(trigger, tip, update);
	});

	const labelTones: Record<TooltipRowTone, string> = {
		neutral: 'text-text-faint',
		xp: 'text-text-faint',
		danger: 'text-text-danger'
	};

	const valueTones: Record<TooltipRowTone, string> = {
		neutral: 'text-text-body',
		xp: 'text-text-xp',
		danger: 'text-text-danger'
	};

	/*
	 * Move the rendered node to the end of <body> so no ancestor's overflow or
	 * stacking context can clip or bury it. Svelte still owns the node — this
	 * action only relocates it, and the `{#if open}` block tears it down.
	 */
	function teleport(node: HTMLElement) {
		document.body.appendChild(node);
		return { destroy() {} };
	}
</script>

<!--
	The wrapper only exists to own the trigger and the open/close handlers;
	`role="group"` is the least-surprising role that lets a static element carry
	them. It stays unlabelled because the control inside already names itself —
	everything the panel adds is either supplementary or repeated as visible text
	on the control. The panel itself is teleported to <body> (see `teleport`).
-->
<span
	role="group"
	class="relative inline-flex"
	bind:this={anchor}
	onmouseenter={() => (open = true)}
	onmouseleave={() => (open = false)}
	onfocusin={() => (open = true)}
	onfocusout={() => (open = false)}
>
	{@render children?.()}

	{#if open}
		<!--
			Positioned with left/top rather than transform so the oi-pop entrance
			animation can keep animating `transform: scale(...)` without fighting
			floating-ui's translate.
		-->
		<span
			use:teleport
			bind:this={panel}
			role="tooltip"
			style:left="{x}px"
			style:top="{y}px"
			class={cn(
				'pointer-events-none fixed z-50 grid max-w-65 min-w-42 animate-[oi-pop_var(--dur-fast)_var(--ease-out)] gap-(--sp-3) rounded-md border border-line-strong bg-surface-overlay p-(--sp-5) shadow-(--shadow-pop) backdrop-blur-[10px] backdrop-saturate-115'
			)}
		>
			<span class="oi-display-sm text-text-strong">{title}</span>
			{#if meta}
				<span class="oi-body-sm text-text-muted">{meta}</span>
			{/if}
			{#if rows.length > 0}
				<span class="grid gap-(--sp-2) border-t border-line-hairline pt-(--sp-3)">
					{#each rows as row (row.label)}
						<span class="flex justify-between gap-(--sp-5)">
							<span class={cn('oi-body-sm', labelTones[row.tone ?? 'neutral'])}>{row.label}</span>
							<span class={cn('oi-num-sm', valueTones[row.tone ?? 'neutral'])}>{row.value}</span>
						</span>
					{/each}
				</span>
			{/if}
		</span>
	{/if}
</span>
