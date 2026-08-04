<script lang="ts" module>
	import { type VariantProps, tv } from 'tailwind-variants';
	import { cn, type WithElementRef } from '$lib/utils/stylingUtils.js';
	import type { Snippet } from 'svelte';
	import type { HTMLAnchorAttributes, HTMLButtonAttributes } from 'svelte/elements';

	/*
	 * OpenIdle Design System button (Button.jsx). Chakra Petch labels, uppercase
	 * and tracked via the oi-label-* classes; press = 1px downward nudge; focus
	 * = a 2px --focus-ring outline 2px out. Variants are the DS vocabulary
	 * (primary / secondary / ghost / danger), secondary by default — the design
	 * leans on un-varianted buttons for its secondary look.
	 */
	export const buttonVariants = tv({
		base: 'group/button inline-flex shrink-0 select-none items-center justify-center gap-(--sp-3) rounded-sm border border-transparent whitespace-nowrap transition-[background-color,color] duration-(--dur-fast) ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring active:not-aria-[haspopup]:translate-y-px disabled:pointer-events-none disabled:opacity-42 [&_svg]:pointer-events-none [&_svg]:shrink-0',
		variants: {
			variant: {
				primary:
					'border-white/14 bg-action-primary text-action-primary-text shadow-[inset_0_1px_0_rgba(255,255,255,.25),var(--shadow-card)] hover:bg-action-primary-hover',
				secondary:
					'border-line-soft bg-surface-card text-text-body shadow-(--shadow-card) hover:bg-surface-card-hover hover:text-text-strong',
				ghost: 'text-text-muted hover:bg-action-quiet hover:text-text-body',
				danger:
					'border-white/12 bg-(--crimson-600) text-text-strong shadow-[inset_0_1px_0_rgba(255,255,255,.18),var(--shadow-card)] hover:bg-(--crimson-500)'
			},
			size: {
				sm: "oi-label-sm rounded-xs px-2.5 py-1.25 [&_svg:not([class*='size-'])]:size-3",
				md: "oi-label-md px-(--pad-control-x) py-(--pad-control-y) [&_svg:not([class*='size-'])]:size-3.5",
				lg: "oi-label-md px-(--sp-7) py-2.75 [&_svg:not([class*='size-'])]:size-4"
			}
		},
		defaultVariants: {
			variant: 'secondary',
			size: 'md'
		}
	});

	export type ButtonVariant = VariantProps<typeof buttonVariants>['variant'];
	export type ButtonSize = VariantProps<typeof buttonVariants>['size'];

	/*
	 * The one case where a `type` alias is required instead of `interface Props`:
	 * the component renders both <button> and <a>, so its props must intersect
	 * the two DOM element attribute sets, which an interface cannot extend.
	 */
	export type Props = WithElementRef<HTMLButtonAttributes> &
		WithElementRef<HTMLAnchorAttributes> & {
			variant?: ButtonVariant;
			size?: ButtonSize;
			children?: Snippet;
		};
</script>

<script lang="ts">
	let {
		class: className,
		variant = 'secondary',
		size = 'md',
		ref = $bindable(null),
		href = undefined,
		type = 'button',
		disabled,
		children,
		...restProps
	}: Props = $props();
</script>

{#if href}
	<a
		bind:this={ref}
		data-slot="button"
		class={cn(buttonVariants({ variant, size }), className)}
		href={disabled ? undefined : href}
		aria-disabled={disabled}
		role={disabled ? 'link' : undefined}
		tabindex={disabled ? -1 : undefined}
		{...restProps}
	>
		{@render children?.()}
	</a>
{:else}
	<button
		bind:this={ref}
		data-slot="button"
		class={cn(buttonVariants({ variant, size }), className)}
		{type}
		{disabled}
		{...restProps}
	>
		{@render children?.()}
	</button>
{/if}
