<script lang="ts">
	import { cn } from '$lib/utils/stylingUtils';
	import type { HTMLSelectAttributes } from 'svelte/elements';
	import type { Snippet } from 'svelte';

	/*
	 * A native <select> wearing Input's clothes.
	 *
	 * The project has no Select component and shadcn-svelte's is a listbox built
	 * out of bits-ui — six more files and a supply-chain review for a control this
	 * page uses a dozen times over. The class string is Input's, so a dropdown and
	 * a text field sitting side by side in the same form line up.
	 */
	interface Props extends HTMLSelectAttributes {
		value?: string;
		children?: Snippet;
	}

	let { value = $bindable(''), class: className, children, ...rest }: Props = $props();
</script>

<select
	bind:value
	class={cn(
		'oi-body-md w-full min-w-0 appearance-none rounded-sm border border-line-soft bg-surface-inset px-(--pad-control-x) py-(--pad-control-y) text-text-strong shadow-(--inset-well) transition-[border-color,color] duration-(--dur-fast) ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:cursor-not-allowed disabled:opacity-42',
		className
	)}
	{...rest}
>
	{@render children?.()}
</select>
