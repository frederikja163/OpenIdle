<script lang="ts">
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System progress meter (Meter.jsx). A recessed track behind
	 * a filled bar; the fill eases on a linear curve because eased motion would
	 * misrepresent constant time. `--meter-*` tokens are not bridged into the
	 * Tailwind theme, so the fill tone uses the CSS-variable shorthand.
	 */
	type MeterTone = 'skill' | 'xp' | 'action' | 'danger';
	type MeterSize = 'sm' | 'md' | 'lg';

	interface Props {
		value?: number;
		max?: number;
		tone?: MeterTone;
		size?: MeterSize;
		label?: string;
		class?: string;
	}

	let {
		value = 0,
		max = 100,
		tone = 'skill',
		size = 'md',
		label,
		class: className
	}: Props = $props();

	// A zero max is "nothing to fill", not a division: 0/0 is NaN, which reaches
	// the style attribute as `width: NaN%` and is dropped, leaving a full-width bar.
	const pct = $derived(max <= 0 ? 0 : Math.max(0, Math.min(100, (value / max) * 100)));

	const heights: Record<MeterSize, string> = {
		sm: 'h-1',
		md: 'h-(--h-meter)',
		lg: 'h-(--h-meter-lg)'
	};

	const tones: Record<MeterTone, string> = {
		skill: 'bg-(--meter-skill)',
		xp: 'bg-(--meter-xp)',
		action: 'bg-(--meter-action)',
		danger: 'bg-(--meter-danger)'
	};
</script>

<div
	role="progressbar"
	aria-label={label}
	aria-valuemin={0}
	aria-valuenow={value}
	aria-valuemax={max}
	class={cn(
		'w-full overflow-hidden rounded-(--radius-full) bg-(--meter-track) shadow-(--inset-meter)',
		heights[size],
		className
	)}
>
	<div
		class={cn(
			'h-full rounded-(--radius-full) shadow-[inset_0_1px_0_rgba(255,255,255,.28)] transition-[width] duration-(--dur-tick) ease-linear',
			tones[tone]
		)}
		style:width={`${pct}%`}
	></div>
</div>
