<script lang="ts">
	import { tick } from 'svelte';
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
	 * A rise eases toward the new width over the transition duration; a drop —
	 * a level-up resetting the XP bar, a payout resetting the action bar — lands
	 * on it at once, because a bar that rewound would read as progress lost.
	 *
	 * Disarming the transition, drawing the new width and re-arming is not enough
	 * on its own: the browser compares the width against the last style it
	 * computed, so unless it computes one while the transition is off, it sees the
	 * new width and the restored transition together and animates the drop anyway.
	 * Reading a layout property is what forces that computation, and it has to
	 * happen after the DOM carries the new width — hence the tick(). A
	 * requestAnimationFrame in its place does not work: the callback runs before
	 * the frame's style recalculation, not after it.
	 */
	let fill = $state<HTMLDivElement | null>(null);
	let drawing = $state(false);
	let previous: number | undefined;

	// A zero max is "nothing to fill", not a division: 0/0 is NaN, which reaches
	// the style attribute as `width: NaN%` and is dropped, leaving a full-width bar.
	const pct = $derived(max <= 0 ? 0 : Math.max(0, Math.min(100, (value / max) * 100)));

	// $effect.pre so the armed state and the width it applies to land in the same
	// DOM update, and reading `value` alone so a change schedules exactly one run.
	$effect.pre(() => {
		const next = value;
		const rising = previous !== undefined && next >= previous;
		previous = next;
		if (rising) {
			drawing = true;
			return;
		}
		// The first run mounts at the current value rather than animating up from
		// an empty bar; a transition never fires on an element's first style
		// anyway, so this is the same path as a drop.
		drawing = false;
		let cancelled = false;
		void tick().then(() => {
			if (cancelled) {
				return;
			}
			void fill?.offsetWidth;
			drawing = true;
		});
		return () => {
			cancelled = true;
		};
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
		bind:this={fill}
		class={cn(
			'h-full rounded-(--radius-full) shadow-[inset_0_1px_0_rgba(255,255,255,.28)]',
			fillMotion,
			tones[tone],
			striped && stripes
		)}
		style:width={`${pct}%`}
	></div>
</div>
