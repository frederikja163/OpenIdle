<script lang="ts">
	import { cn, type WithElementRef } from '$lib/utils/stylingUtils.js';
	import type { HTMLAttributes } from 'svelte/elements';

	// The generator's optional built-in Close button is gone: it renders a
	// `variant="outline"` Button, and this project's Button fork has no such
	// variant. Callers compose their own dismiss button out of Dialog.Close.
	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithElementRef<HTMLAttributes<HTMLDivElement>> = $props();
</script>

<div
	bind:this={ref}
	data-slot="dialog-footer"
	class={cn('flex flex-col-reverse gap-(--gap-stack) sm:flex-row sm:justify-end', className)}
	{...restProps}
>
	{@render children?.()}
</div>
