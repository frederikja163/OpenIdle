<script lang="ts">
	import { cn, type WithElementRef } from '$lib/utils/stylingUtils.js';
	import type { Snippet } from 'svelte';
	import type { HTMLAttributes } from 'svelte/elements';

	interface Props extends WithElementRef<HTMLAttributes<HTMLDivElement>> {
		children?: Snippet;
	}

	let { ref = $bindable(null), class: className, children, ...restProps }: Props = $props();
</script>

<div
	bind:this={ref}
	data-slot="card-footer"
	class={cn('flex items-center px-(--card-spacing) [.border-t]:pt-(--card-spacing)', className)}
	{...restProps}
>
	{@render children?.()}
</div>
