<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import type { Snippet } from 'svelte';
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
{@render children?.()}
