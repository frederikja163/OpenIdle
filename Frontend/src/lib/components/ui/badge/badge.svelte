<script lang="ts" module>
	import { type VariantProps, tv } from 'tailwind-variants';

	/*
	 * OpenIdle Design System badge (Badge.jsx): 3px/7px padding chip, radius-xs,
	 * oi-label-sm uppercase, 11px icon. All six DS tones are exact token
	 * equivalences, kept so the variants cannot drift from the design system.
	 */
	export const badgeVariants = tv({
		base: 'oi-label-sm inline-flex w-fit shrink-0 items-center gap-(--sp-2) overflow-hidden rounded-xs border px-1.75 py-0.75 whitespace-nowrap focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring [&>svg]:pointer-events-none [&>svg]:size-2.75',
		variants: {
			variant: {
				neutral: 'border-line-soft bg-(--ink-300)/10 text-text-muted',
				accent: 'border-verdant-400/35 bg-verdant-500/16 text-verdant-300',
				xp: 'border-amber-400/32 bg-amber-500/16 text-amber-300',
				info: 'border-(--azure-400)/32 bg-(--azure-500)/16 text-(--azure-400)',
				rare: 'border-(--amethyst-400)/34 bg-(--amethyst-500)/18 text-(--amethyst-400)',
				danger: 'border-(--crimson-400)/32 bg-(--crimson-500)/16 text-text-danger'
			}
		},
		defaultVariants: {
			variant: 'neutral'
		}
	});

	export type BadgeVariant = VariantProps<typeof badgeVariants>['variant'];
</script>

<script lang="ts">
	import { cn, type WithElementRef } from '$lib/utils/stylingUtils.js';
	import type { Snippet } from 'svelte';
	import type { HTMLAnchorAttributes } from 'svelte/elements';

	interface Props extends WithElementRef<HTMLAnchorAttributes> {
		variant?: BadgeVariant;
		children?: Snippet;
	}

	let {
		ref = $bindable(null),
		href,
		class: className,
		variant = 'neutral',
		children,
		...restProps
	}: Props = $props();
</script>

<svelte:element
	this={href ? 'a' : 'span'}
	bind:this={ref}
	data-slot="badge"
	{href}
	class={cn(badgeVariants({ variant }), className)}
	{...restProps}
>
	{@render children?.()}
</svelte:element>
