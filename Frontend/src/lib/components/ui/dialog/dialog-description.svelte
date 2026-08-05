<script lang="ts">
	import { Dialog as DialogPrimitive } from 'bits-ui';
	import { cn } from '$lib/utils/stylingUtils.js';

	// The generator's descendant-anchor styling is dropped: utilities.css already
	// gives links their colour and underline in a base layer.
	let {
		ref = $bindable(null),
		class: className,
		...restProps
	}: DialogPrimitive.DescriptionProps = $props();
</script>

<DialogPrimitive.Description
	bind:ref
	data-slot="dialog-description"
	class={cn('oi-body-sm text-pretty text-text-muted', className)}
	{...restProps}
/>
