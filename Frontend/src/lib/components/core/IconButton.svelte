<script lang="ts">
	import type { HTMLButtonAttributes } from 'svelte/elements';
	import type { IconComponent } from '$lib/components/icon';
	import { buttonVariants } from '$lib/components/ui/button';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System icon button (IconButton.jsx): a square, chromeless
	 * control for panel-header affordances. `label` is required rather than
	 * optional because the button renders no text — it is the only thing naming
	 * the control to a screen reader, and it doubles as the hover title.
	 *
	 * The chromeless treatment itself is Button's `ghost` variant, taken from
	 * there rather than restated, so hover, focus, press and disabled cannot
	 * drift between the two. What this file still owns is the square geometry
	 * Button has no size for: the box, its radius and the glyph inside it.
	 */
	type IconButtonSize = 'sm' | 'md' | 'lg';

	interface Props extends HTMLButtonAttributes {
		icon: IconComponent;
		label: string;
		size?: IconButtonSize;
		active?: boolean;
	}

	let {
		icon: ButtonIcon,
		label,
		size = 'md',
		active = false,
		class: className,
		...rest
	}: Props = $props();

	const boxes: Record<IconButtonSize, string> = {
		sm: 'size-6.5',
		md: 'size-8',
		lg: 'size-9.5'
	};

	// Spelled out per size rather than composed, because Tailwind only sees class
	// names it can read whole in the source. The values are the design's 13/16/18px
	// glyphs, restated under the same modifier Button sizes its own icons with so
	// the two resolve to one declaration instead of fighting.
	const glyphs: Record<IconButtonSize, string> = {
		sm: "[&_svg:not([class*='size-'])]:size-3.25",
		md: "[&_svg:not([class*='size-'])]:size-4",
		lg: "[&_svg:not([class*='size-'])]:size-4.5"
	};
</script>

<button
	type="button"
	aria-label={label}
	title={label}
	class={cn(
		buttonVariants({ variant: 'ghost', size }),
		'rounded-sm p-0',
		boxes[size],
		glyphs[size],
		// The accent state has to hold through hover as well, or the toggle drops
		// back to the quiet fill under the cursor that is about to switch it off.
		active &&
			'border-line-accent bg-surface-active text-text-accent not-disabled:hover:bg-surface-active',
		className
	)}
	{...rest}
>
	<ButtonIcon />
</button>
