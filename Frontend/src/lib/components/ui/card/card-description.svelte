<script lang="ts">
	import { cn, type WithElementRef } from '$lib/utils/stylingUtils.js';
	import type { Snippet } from 'svelte';
	import type { HTMLAttributes } from 'svelte/elements';

	interface Props extends WithElementRef<HTMLAttributes<HTMLParagraphElement>> {
		children?: Snippet;
	}

	let { ref = $bindable(null), class: className, children, ...restProps }: Props = $props();
</script>

<p
	bind:this={ref}
	data-slot="card-description"
	class={cn('oi-body-sm text-text-muted', className)}
	{...restProps}
>
	{@render children?.()}
</p>
