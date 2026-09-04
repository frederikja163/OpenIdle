<script lang="ts">
	import { onMount } from 'svelte';
	import { connectionState } from '$lib/state/session.svelte';
	import { loadBackendVersion, versionState } from '$lib/state/version.svelte';
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

	// Asks on mount and whenever the pointed-at backend may have changed, i.e.
	// when the socket (re)opens. Each ask is a fresh fetch; a failure ends in
	// 'failed' rather than retrying, so this stays quiet once answered or failed.
	onMount(() => {
		void loadBackendVersion();
	});
	$effect(() => {
		if (connectionState.status === 'open') {
			void loadBackendVersion();
		}
	});

	const backend = $derived(
		versionState.status === 'loaded' && versionState.backend
			? formatVersion(versionState.backend)
			: versionState.status === 'failed' || versionState.status === 'idle'
				? 'unavailable'
				: '…'
	);
</script>

<!--
	A <footer> rather than a Row, for the landmark. No role="status": the
	connection banner in the (auth) chrome is the page's one live region, and a
	build number is not worth announcing.
-->
<footer
	data-testid="version-footer"
	class={cn('flex flex-wrap items-baseline gap-(--sp-5) text-text-faint', className)}
>
	<span class="oi-label-sm">OpenIdle</span>
	<span class="oi-body-sm">
		frontend <span class="oi-num-sm">{formatVersion(versionState.frontend)}</span>
	</span>
	<span class="oi-body-sm">
		backend <span class="oi-num-sm">{backend}</span>
	</span>
</footer>
