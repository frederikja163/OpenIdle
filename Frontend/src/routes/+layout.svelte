<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import type { Snippet } from 'svelte';
	import Column from '$lib/components/layout/Column.svelte';
	import { wireSession } from '$lib/state/wiring';

	interface Props {
		children?: Snippet;
	}

	let { children }: Props = $props();

	// Once, at the root: the socket has to be connected to the stores before any
	// route uses them, and an effect never runs during SSR so there is no need to
	// guard on `browser`.
	$effect(() => {
		wireSession();
	});
</script>

<svelte:head><link rel="icon" href={favicon} /></svelte:head>
<!--
	A viewport-tall column, so a page can push its version footer to the bottom
	(`grow` on the page, `mt-auto` on the footer) and still grow past the fold
	when it is long.
-->
<Column class="min-h-dvh">
	{@render children?.()}
</Column>
