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
		striped?: boolean;
		label?: string;
		class?: string;
		/**
		 * How a rise in value draws its fill. 'tick' eases over the design-system
		 * tick and suits sparse updates (the XP meter, which moves once per game
		 * tick); 'sweep' eases over the board loop's interval, so a meter that
		 * steps every loop tick reads as one constant fill instead of jumping.
		 * Either way a drop to a lower value snaps instantly rather than
		 * rewinding.
		 */
		transition?: 'tick' | 'sweep';
	}

	let {
		value = 0,
		max = 100,
		tone = 'skill',
		size = 'md',
		striped = false,
		label,
		class: className,
		transition = 'tick'
	}: Props = $props();

	/*
	 * The drawn width trails `value`: a rise eases toward it over the transition
	 * duration, a drop skips the easing and lands on the new value at once. The
	 * two are told apart from the last value drawn, and the transition is
	 * re-armed on the next frame after a snap so the following rise can animate.
	 */
	let shown = $state(0);
	let drawing = $state(false);
	let seeded = false;

	const pct = $derived(max <= 0 ? 0 : Math.max(0, Math.min(100, (shown / max) * 100)));

	$effect(() => {
		// First run: appear at the value we mount at, without animating up from
		// an empty bar. `drawing` is only armed afterwards.
		if (!seeded) {
			seeded = true;
			shown = value;
			requestAnimationFrame(() => {
				drawing = true;
			});
			return;
		}
		if (value === shown) return;
		if (value < shown) {
			drawing = false;
			shown = value;
			requestAnimationFrame(() => {
				drawing = true;
			});
		} else {
			drawing = true;
			shown = value;
		}
	});

	const fillMotion = $derived(
		drawing &&
			(transition === 'sweep'
				? 'transition-[width] duration-(--dur-sweep) ease-linear'
				: 'transition-[width] duration-(--dur-tick) ease-linear')
	);

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

	// The sweep rides on top of the tone rather than replacing it: `bg-(--meter-*)`
	// sets background-color and this sets background-image, so the two compose and
	// one stripe declaration serves every tone. The gradient stops stay literal
	// rgba because a colour stop cannot take Tailwind's `/opacity` shorthand.
	const stripes =
		'bg-[image:repeating-linear-gradient(115deg,rgba(255,255,255,.16)_0_6px,transparent_6px_14px)] bg-[length:28px_100%] animate-[oi-sweep_700ms_linear_infinite]';
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
			'h-full rounded-(--radius-full) shadow-[inset_0_1px_0_rgba(255,255,255,.28)]',
			fillMotion,
			tones[tone],
			striped && stripes
		)}
		style:width={`${pct}%`}
	></div>
</div>
