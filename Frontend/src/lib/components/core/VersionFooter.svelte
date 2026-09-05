<script lang="ts">
	import { onMount } from 'svelte';
	import { ensureBackendVersion, versionState } from '$lib/state/version.svelte';
	import { cn } from '$lib/utils/stylingUtils';
	import { formatVersion } from '$lib/utils/version';

	interface Props {
		class?: string;
	}

	let { class: className }: Props = $props();

	/*
	 * Which builds are running, for bug reports: this bundle's and the connected
	 * backend's. Rendered by the login, profiles and debug pages rather than by
	 * a layout, so the game view stays clear of it.
	 */

	// Only the first footer to mount for a given backend actually asks; the
	// re-ask on every socket open lives in wireSession(), so it fires once per
	// connection rather than once per mounted footer.
	onMount(() => {
		void ensureBackendVersion();
	});

	const backend = $derived(
		versionState.backend
			? formatVersion(versionState.backend)
			: versionState.status === 'failed'
				? 'unavailable'
				: '…'
	);
</script>

<!--
	A <footer> rather than a Row, for the landmark. No role="status": the
	connection banner in the (auth) chrome is the page's one live region, and a
	build number is not worth announcing.

	self-stretch spans the page even inside a column that keeps its other
	children to their own width (the login page); the text then centres in it.
-->
<footer
	data-testid="version-footer"
	class={cn(
		'flex flex-wrap items-baseline justify-center gap-(--sp-5) self-stretch text-text-faint',
		className
	)}
>
	<span class="oi-label-sm">OpenIdle</span>
	<span class="oi-body-sm">
		frontend <span class="oi-num-sm">{formatVersion(versionState.frontend)}</span>
	</span>
	<span class="oi-body-sm">
		backend <span class="oi-num-sm">{backend}</span>
	</span>
</footer>
