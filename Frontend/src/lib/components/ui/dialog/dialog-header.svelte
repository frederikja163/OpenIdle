<script lang="ts">
	import { cn, type WithElementRef } from '$lib/utils/stylingUtils.js';
	import type { HTMLAttributes } from 'svelte/elements';

	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithElementRef<HTMLAttributes<HTMLDivElement>> = $props();
</script>

<div
	bind:this={ref}
	data-slot="dialog-header"
	class={cn('flex flex-col gap-(--sp-4)', className)}
	{...restProps}
>
	{@render children?.()}
</div>
