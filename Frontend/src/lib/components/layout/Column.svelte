<script lang="ts">
	import type { SvelteHTMLElements } from 'svelte/elements';
	import { cn } from '$lib/utils/stylingUtils';

	let { children, class: className, ...rest }: SvelteHTMLElements['div'] = $props();
</script>

<div {...rest} class={cn('flex flex-col', className)}>
	{@render children?.()}
</div>
