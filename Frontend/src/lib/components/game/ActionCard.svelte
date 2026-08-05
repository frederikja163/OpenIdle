<script lang="ts" module>
	import type { Component } from 'svelte';
	import type { Rarity } from '$lib/components/game/ItemArt.svelte';

	/*
	 * OpenIdle Design System action card (ActionCard.jsx): one runnable action —
	 * its art, yield, tick meter, material cost and the duration/XP footer.
	 *
	 * Three states share the card and must stay distinguishable at a glance:
	 * running carries the board's only --glow-accent, and locked drops to 45%
	 * and refuses the click — whether the lock is a skill level or missing
	 * materials, with `lockedBy: 'items'` reporting the shortfall in its tooltip.
	 */
	export type ActionLock = 'level' | 'items';

	export interface ActionInput {
		id: string;
		name: string;
		glyph: Component<{ size?: number | string }>;
		rarity?: Rarity;
		qty: number;
		/** How many the visitor actually holds; filled in by the board. */
		have?: number;
	}
</script>

<script lang="ts">
	import ChevronsUp from '@lucide/svelte/icons/chevrons-up';
	import Lock from '@lucide/svelte/icons/lock';
	import Package from '@lucide/svelte/icons/package';
	import PackageX from '@lucide/svelte/icons/package-x';
	import Pause from '@lucide/svelte/icons/pause';
	import Timer from '@lucide/svelte/icons/timer';
	import Meter from '$lib/components/core/Meter.svelte';
	import Tooltip, { type TooltipRow } from '$lib/components/feedback/Tooltip.svelte';
	import ItemArt from '$lib/components/game/ItemArt.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { cn } from '$lib/utils/stylingUtils';

	interface Props {
		name: string;
		glyph: Component<{ size?: number | string }>;
		rarity?: Rarity;
		yieldQty?: number;
		inputs?: ActionInput[];
		duration: string;
		xp?: number;
		running?: boolean;
		progress?: number;
		locked?: boolean;
		lockedAt?: number;
		lockedBy?: ActionLock;
		lockedSkill?: string;
		skillLevel?: number;
		onclick?: () => void;
	}

	let {
		name,
		glyph,
		rarity = 'common',
		yieldQty = 1,
		inputs,
		duration,
		xp = 1,
		running = false,
		progress = 0,
		locked = false,
		lockedAt,
		lockedBy = 'level',
		lockedSkill,
		skillLevel,
		onclick
	}: Props = $props();

	const missing = $derived((inputs ?? []).filter((input) => (input.have ?? 0) < input.qty));

	const lockRows: TooltipRow[] = $derived(
		lockedBy === 'items'
			? missing.map((input) => ({
					label: input.name,
					value: `${input.have ?? 0} / ${input.qty}`,
					tone: 'danger' as const
				}))
			: lockedAt != null
				? [
						{
							label: `${lockedSkill ?? 'Skill'} level`,
							value: `${skillLevel ?? 0} / ${lockedAt}`,
							tone: 'danger' as const
						}
					]
				: []
	);
</script>

{#snippet card()}
	<button
		type="button"
		aria-disabled={locked || undefined}
		onclick={locked ? undefined : onclick}
		class={cn(
			'flex w-(--size-action-card) flex-col overflow-hidden rounded-md border text-left transition-[background-color,box-shadow,translate,opacity] duration-(--dur-fast) ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
			locked
				? 'cursor-not-allowed border-line-soft bg-surface-card opacity-45 shadow-(--shadow-card)'
				: 'cursor-pointer',
			running && 'border-line-accent bg-surface-active shadow-(--glow-accent)',
			!locked &&
				!running &&
				'hover:-translate-y-px hover:bg-surface-card-hover hover:shadow-(--shadow-card-hover)',
			!locked && !running && 'border-line-soft bg-surface-card shadow-(--shadow-card)'
		)}
	>
		<div class="relative grid place-items-center p-(--pad-card)">
			<ItemArt {glyph} {rarity} size={92} width="100%" />
			{#if running}
				<span
					class="absolute top-2 right-2 inline-flex size-5 items-center justify-center rounded-(--radius-full) bg-verdant-500 text-action-primary-text shadow-(--glow-accent)"
				>
					<Pause size={11} />
				</span>
			{:else if locked}
				<span class="absolute top-2 right-2 text-text-faint">
					{#if lockedBy === 'items'}
						<PackageX size={13} />
					{:else}
						<Lock size={13} />
					{/if}
				</span>
			{/if}
		</div>

		<div class="grid gap-(--sp-3) px-(--pad-card) pb-(--sp-4)">
			<Row
				class={cn(
					'oi-num-sm items-center gap-(--sp-3)',
					yieldQty > 1 ? 'text-text-accent' : 'text-text-muted'
				)}
			>
				<Package size={11} />×{yieldQty}
			</Row>

			<!-- Two lines are reserved whether the name needs them or not, so the
			     meters and footers line up across a row of mixed-length names. -->
			<div
				class={cn(
					'oi-display-sm line-clamp-2 min-h-[2.4em]',
					locked ? 'text-text-faint' : 'text-text-strong'
				)}
			>
				{name}
			</div>

			<Row class="min-h-4.5 items-center">
				{#if locked}
					<span
						class={cn('oi-body-sm', lockedBy === 'items' ? 'text-text-danger' : 'text-text-faint')}
					>
						{#if lockedBy === 'items'}
							Missing materials
						{:else if lockedAt}
							Unlocks at level {lockedAt}
						{:else}
							Locked
						{/if}
					</span>
				{:else}
					<Meter
						value={running ? progress : 0}
						tone="action"
						size="sm"
						striped={running}
						label="{name} progress"
					/>
				{/if}
			</Row>
		</div>

		{#if inputs && inputs.length > 0}
			<Row class="flex-wrap gap-1 px-(--pad-card) pb-(--sp-4)">
				{#each inputs as input (input.id)}
					{@const isShort = (input.have ?? 0) < input.qty}
					<span
						title="{input.name} — {input.have ?? 0}/{input.qty}"
						class={cn(
							'inline-flex items-center gap-0.75 rounded-xs border py-0.5 pr-1 pl-0.5',
							isShort
								? 'border-[rgba(236,118,118,.45)] bg-[rgba(210,71,71,.18)]'
								: 'border-line-hairline bg-[rgba(168,184,199,.08)]'
						)}
					>
						<ItemArt glyph={input.glyph} rarity={input.rarity} size={18} variant="bare" />
						<span class={cn('oi-num-sm', isShort ? 'text-text-danger' : 'text-text-muted')}>
							{input.qty}
						</span>
					</span>
				{/each}
			</Row>
		{/if}

		<div class="mt-auto grid grid-cols-2 border-t border-line-hairline">
			<span
				class="oi-num-sm inline-flex items-center justify-center gap-1.25 py-1.75 text-text-muted"
			>
				<Timer size={11} />{duration}
			</span>
			<span
				class="oi-num-sm inline-flex items-center justify-center gap-1.25 border-l border-line-hairline py-1.75 text-text-xp"
			>
				<ChevronsUp size={11} />{xp} XP
			</span>
		</div>
	</button>
{/snippet}

{#if locked}
	<Tooltip
		title="Recipe locked"
		meta={lockedBy === 'items' ? 'Missing materials' : 'Required skill levels'}
		rows={lockRows}
	>
		{@render card()}
	</Tooltip>
{:else}
	{@render card()}
{/if}
