<script lang="ts">
	import { onMount } from 'svelte';
	import { resolve } from '$app/paths';
	import ArrowLeft from '@lucide/svelte/icons/arrow-left';
	import VersionFooter from '$lib/components/core/VersionFooter.svelte';
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { ensureProtocolSchema, schemaState } from '$lib/debug/schema.svelte';
	import { recordTraffic } from '$lib/debug/traffic.svelte';
	import ConnectionPanel from './components/ConnectionPanel.svelte';
	import RequestBuilder from './components/RequestBuilder.svelte';
	import SessionPanel from './components/SessionPanel.svelte';
	import TrafficLog from './components/TrafficLog.svelte';

	/*
	 * A protocol console for the websocket — pick a request, fill in a form built from its
	 * declared properties, watch the frames.
	 *
	 * The catalogue is fetched from the connected backend's GET /schema, rather than taken
	 * from RequestMap in $lib/ws/protocol — which is hand-maintained and already behind the
	 * contract. That is the point of the page: it can exercise a request the rest of the
	 * frontend has never heard of, and it describes the backend it is pointed at rather than
	 * the revision this bundle was built from.
	 */

	// onMount rather than an $effect, like VersionFooter: the ask belongs to the page
	// opening, and nothing later on the page should be able to re-trigger it. Pointing
	// the app elsewhere re-asks from ConnectionPanel instead.
	onMount(() => {
		void ensureProtocolSchema();
	});

	// Effects never run during SSR, so the socket tap is browser-only without a guard.
	$effect(() => recordTraffic());

	const catalogue = $derived(
		schemaState.protocol
			? `${schemaState.protocol.requests.length} requests`
			: schemaState.status === 'failed'
				? 'contract unavailable'
				: 'reading the contract…'
	);
</script>

<svelte:head><title>OpenIdle — protocol console</title></svelte:head>

<Column class="oi-board h-screen gap-(--gutter-app) p-(--gutter-app)">
	<Row class="items-center gap-(--sp-5)">
		<h1 class="oi-display-md text-text-strong">Protocol console</h1>
		<span class="oi-body-md text-text-muted">{catalogue}</span>
		<Button variant="ghost" size="sm" class="ml-auto" href={resolve('/profiles')}>
			<ArrowLeft />
			Back to app
		</Button>
	</Row>

	<!-- Two columns on a wide screen: build on the left, watch on the right. The page owns
	     the height so each side scrolls itself rather than the document. -->
	<div class="grid min-h-0 grow gap-(--gutter-app) lg:grid-cols-2">
		<Column
			class="oi-scroll min-h-0 gap-(--gutter-panel) overflow-y-auto rounded-lg border border-line-soft bg-surface-panel p-(--gutter-panel) shadow-(--shadow-panel)"
		>
			<ConnectionPanel />
			<hr class="border-line-soft" />
			<SessionPanel />
			<hr class="border-line-soft" />
			<RequestBuilder />
		</Column>

		<Column
			class="min-h-0 rounded-lg border border-line-soft bg-surface-panel p-(--gutter-panel) shadow-(--shadow-panel)"
		>
			<TrafficLog />
		</Column>
	</div>

	<!-- The grid above is min-h-0 grow, so it simply gives up the footer's height. -->
	<VersionFooter />
</Column>
