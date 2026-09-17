<script lang="ts">
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { ensureProtocolSchema, schemaState } from '$lib/debug/schema.svelte';
	import { connectionState } from '$lib/state/session.svelte';
	import { logout } from '$lib/state/user.svelte';
	import { ensureBackendVersion } from '$lib/state/version.svelte';
	import {
		clearWsUrl,
		getDefaultWsUrl,
		getWsClient,
		getWsUrl,
		hasWsUrlOverride,
		setWsUrl
	} from '$lib/ws/client';

	/*
	 * Points the whole app at a backend.
	 *
	 * App-wide rather than a socket of its own, so a request sent here and a
	 * request sent by /profiles are the same session: the backend session *is* the
	 * connection, so a private debug socket would be logged into a different
	 * session than the one the rest of the app is looking at.
	 */
	let url = $state(getWsUrl());
	let overridden = $state(hasWsUrlOverride());
	let applyError = $state<string | null>(null);

	function apply(): void {
		applyError = null;
		let target: URL;
		try {
			target = new URL(url);
		} catch {
			applyError = `'${url}' is not a URL.`;
			return;
		}
		if (target.protocol !== 'ws:' && target.protocol !== 'wss:') {
			applyError = 'The URL must start with ws:// or wss://.';
			return;
		}
		// The old session belongs to the old backend and cannot be carried across, so
		// it is ended the same way the app ends one anywhere else.
		logout();
		setWsUrl(url);
		overridden = true;
		// The footer would otherwise keep the old backend's build until a socket
		// opens; the new address can be asked over HTTP right away. The contract
		// likewise: it belongs to the backend, so it is re-asked after setWsUrl,
		// which is what getApiUrl derives the new address from.
		void ensureBackendVersion();
		void ensureProtocolSchema();
	}

	function reset(): void {
		applyError = null;
		logout();
		clearWsUrl();
		url = getWsUrl();
		overridden = false;
		void ensureBackendVersion();
		void ensureProtocolSchema();
	}

	const contract = $derived(
		schemaState.status === 'loaded'
			? 'loaded'
			: schemaState.status === 'failed'
				? 'unavailable'
				: '…'
	);
</script>

<Column class="gap-(--sp-6)">
	<Row class="items-baseline gap-(--sp-5)">
		<span class="oi-label-md text-text-strong">Backend</span>
		<span class="oi-body-sm text-text-muted">{connectionState.status}</span>
		{#if overridden}
			<span class="oi-body-sm text-text-accent">overridden for this browser</span>
		{/if}
	</Row>

	<Row class="flex-wrap items-center gap-(--sp-5)">
		<Input class="max-w-[28rem]" bind:value={url} placeholder={getDefaultWsUrl()} />
		<Button variant="primary" onclick={apply}>Apply</Button>
		<Button
			onclick={() =>
				void getWsClient()
					.connect()
					.catch(() => {})}>Connect</Button
		>
		<Button onclick={() => logout()}>Disconnect</Button>
		<Button variant="ghost" onclick={reset} disabled={!overridden}>Reset to default</Button>
	</Row>

	{#if applyError}
		<span role="alert" class="oi-body-sm text-text-danger">{applyError}</span>
	{/if}

	<!--
		The catalogue is fetched from the backend above rather than compiled in, so it is
		that backend's own contract and the two cannot disagree. Said on screen because it
		is the counterpart to the address: changing one changes the other, and a contract
		that failed to load is why the request form below would be empty.

		The console can therefore no longer offer a request the backend has never heard of —
		the frame editor still can, by hand.
	-->
	<Row class="flex-wrap items-center gap-(--sp-5)">
		<span class="oi-label-sm text-text-muted">Contract</span>
		<span class="oi-body-sm text-text-faint">
			GET /schema on this backend — <span class="oi-num-sm">{contract}</span>
		</span>
	</Row>
</Column>
