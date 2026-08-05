<script lang="ts" module>
	/*
	 * OpenIdle Design System item art (ItemArt.jsx): the recessed well every item
	 * sits in, tinted and edged by rarity. No artwork was ever supplied for the
	 * game's items, so the design's own fallback is a Lucide glyph — see the
	 * source-fidelity note in the kit's README.
	 */
	export type Rarity = 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary';

	/*
	 * The design's three call sites each strip the well down a different amount;
	 * naming them as variants keeps that in one place instead of having callers
	 * cancel the background and shadow back off again.
	 *   well   — the full treatment, on action cards
	 *   tinted — rarity tint only, inside an inventory slot that is already a card
	 *   bare   — just the glyph, for the input chips on a crafting recipe
	 */
	export type ItemArtVariant = 'well' | 'tinted' | 'bare';

	/*
	 * Ported literally from the design because a gradient colour stop cannot take
	 * Tailwind's `/opacity` shorthand — `var(--verdant-500)/16` is not valid CSS
	 * inside `radial-gradient()`. The values are the rarity hues from colors.css
	 * at the alphas the design specifies.
	 */
	const tints: Record<Rarity, string> = {
		common: 'rgba(168,184,199,.10)',
		uncommon: 'rgba(79,170,94,.16)',
		rare: 'rgba(58,144,212,.18)',
		epic: 'rgba(138,95,208,.20)',
		legendary: 'rgba(221,149,32,.20)'
	};

	const edges: Record<Rarity, string> = {
		common: 'var(--line-soft)',
		uncommon: 'rgba(104,196,119,.4)',
		rare: 'rgba(107,178,234,.42)',
		epic: 'rgba(169,133,230,.45)',
		legendary: 'rgba(240,179,68,.45)'
	};
</script>

<script lang="ts">
	import type { Component } from 'svelte';
	import { cn } from '$lib/utils/stylingUtils';

	interface Props {
		glyph: Component<{ size?: number | string }>;
		/** Box edge in px; also sets the glyph at 46% of it. */
		size?: number;
		/** Overrides the box width — the action card's well spans the whole card. */
		width?: string;
		/** Overrides the box height — an inventory slot's art fills the slot. */
		height?: string;
		rarity?: Rarity;
		variant?: ItemArtVariant;
		class?: string;
	}

	let {
		glyph: ArtGlyph,
		size = 64,
		width,
		height,
		rarity = 'common',
		variant = 'well',
		class: className
	}: Props = $props();

	const shapes: Record<ItemArtVariant, string> = {
		well: 'rounded-sm bg-surface-inset',
		tinted: 'rounded-xs',
		bare: ''
	};
</script>

<div
	class={cn(
		'grid shrink-0 place-items-center overflow-hidden text-(--ink-500)',
		shapes[variant],
		className
	)}
	style:width={width ?? `${size}px`}
	style:height={height ?? `${size}px`}
	style:background-image={variant === 'bare'
		? undefined
		: `radial-gradient(70% 70% at 50% 35%,${tints[rarity]},transparent 78%)`}
	style:box-shadow={variant === 'well'
		? `var(--inset-well),inset 0 0 0 1px ${edges[rarity]}`
		: undefined}
>
	<ArtGlyph size={Math.round(size * 0.46)} />
</div>
