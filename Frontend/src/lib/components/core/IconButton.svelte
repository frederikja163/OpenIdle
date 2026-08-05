<script lang="ts">
	import type { Component } from 'svelte';
	import type { HTMLButtonAttributes } from 'svelte/elements';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System icon button (IconButton.jsx): a square, chromeless
	 * control for panel-header affordances. `label` is required rather than
	 * optional because the button renders no text — it is the only thing naming
	 * the control to a screen reader, and it doubles as the hover title.
	 */
	type IconButtonSize = 'sm' | 'md' | 'lg';

	interface Props extends HTMLButtonAttributes {
		icon: Component<{ size?: number | string }>;
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

	const glyphs: Record<IconButtonSize, number> = {
		sm: 13,
		md: 16,
		lg: 18
	};
</script>

<button
	type="button"
	aria-label={label}
	title={label}
	class={cn(
		'inline-flex cursor-pointer items-center justify-center rounded-sm border border-transparent transition-[background-color,color] duration-(--dur-fast) ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
		boxes[size],
		active
			? 'border-line-accent bg-surface-active text-text-accent'
			: 'text-text-muted hover:bg-action-quiet hover:text-text-body',
		className
	)}
	{...rest}
>
	<ButtonIcon size={glyphs[size]} />
</button>
