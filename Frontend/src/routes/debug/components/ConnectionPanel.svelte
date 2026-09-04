<script lang="ts">
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { connectionState } from '$lib/state/session.svelte';
	import { logout } from '$lib/state/user.svelte';
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
	}

	function reset(): void {
		applyError = null;
		logout();
		clearWsUrl();
		url = getWsUrl();
		overridden = false;
	}
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
		The catalogue is compiled in, not fetched, so it describes the repository's contract
		rather than whatever the connected backend was built from. Said on screen because
		nothing can detect the mismatch: a request this page offers may simply not exist on
		the other end, and the reply to that is an error like any other.
	-->
	<Row class="flex-wrap items-center gap-(--sp-5)">
		<span class="oi-label-sm text-text-muted">Contract</span>
		<span class="oi-body-sm text-text-faint">
			types.xml, compiled in at build time — a backend built from another revision may not match it
		</span>
	</Row>
</Column>
