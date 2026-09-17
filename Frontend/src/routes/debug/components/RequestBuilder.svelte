<script lang="ts">
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { buildPayload, createFields, PayloadError, type FieldNode } from '$lib/debug/formModel';
	import { loadProtocolSchema, schemaState } from '$lib/debug/schema.svelte';
	import type { ProtocolSchema, SchemaRequest } from '$lib/debug/schema';
	import { getWsClient } from '$lib/ws/client';
	import { MAX_MESSAGE_BYTES } from '$lib/ws/protocol';
	import PropertyField from './PropertyField.svelte';
	import Select from './Select.svelte';

	/*
	 * The catalogue is fetched from the backend, so it arrives after the first render and is
	 * replaced whenever the app is pointed somewhere else. This component owns the guard on
	 * it: everything below only renders once there is one, which is what lets PropertyField
	 * take a plain ProtocolSchema rather than a nullable one.
	 */
	const protocol = $derived(schemaState.protocol);
	const requests = $derived(
		protocol ? [...protocol.requests].sort((a, b) => a.typeName.localeCompare(b.typeName)) : []
	);

	let selectedType = $state('');
	let fields = $state<FieldNode[]>([]);
	let draft = $state('');
	/**
	 * The id this frame will go out under. Taken from the client rather than
	 * invented here, so the number in the editor is the real one — and shown at
	 * all so that it can be changed: a duplicated or absurd id is a case worth
	 * being able to stage.
	 *
	 * Null until the first render, and again after each send. Reserved lazily
	 * because render() is the first thing to run in the browser, and the client
	 * must not be built during SSR.
	 */
	let requestId = $state<number | null>(null);
	/**
	 * Whether the JSON below has been hand-edited. Once it has, the form stops
	 * writing over it — an edit the user made is worth more than the form's own
	 * rendering of the same thing, and losing it to the next keystroke elsewhere
	 * would make the editor useless.
	 */
	let edited = $state(false);
	let sending = $state(false);
	let result = $state<{ ok: boolean; text: string } | null>(null);

	const selected = $derived(requests.find((request) => request.typeName === selectedType) ?? null);
	// Exactly what will go on the wire, since the draft is sent verbatim.
	const bytes = $derived(new TextEncoder().encode(draft).length);
	const tooLarge = $derived(bytes > MAX_MESSAGE_BYTES);

	function choose(typeName: string): void {
		selectedType = typeName;
		const request = protocol?.requests.find((candidate) => candidate.typeName === typeName);
		fields = request && protocol ? createFields(request.properties, protocol) : [];
		edited = false;
		result = null;
		render();
	}

	/*
	 * Which catalogue the form was built from. Tracked by identity, and deliberately
	 * not $state: a fetch replaces the property objects the field nodes hold, so nodes
	 * built from the previous one would describe a contract that is no longer on
	 * screen. Keeps the chosen request across the rebuild when the new catalogue still
	 * declares it, which is the common case of asking the same backend twice.
	 */
	let builtFrom: ProtocolSchema | null = null;
	$effect(() => {
		if (!protocol || protocol === builtFrom) {
			return;
		}
		builtFrom = protocol;
		choose(
			requests.some((request) => request.typeName === selectedType)
				? selectedType
				: (requests[0]?.typeName ?? '')
		);
	});

	/** Redraws the JSON from the form, unless the JSON has been taken over by hand. */
	function render(): void {
		if (edited || !selected) {
			return;
		}
		requestId ??= getWsClient().reserveRequestId();
		try {
			// Key order matches encodeRequest's, so the editor shows the frame in the
			// shape the rest of the app puts on the wire.
			draft = JSON.stringify(
				{ $type: selected.typeName, requestId, ...buildPayload(fields) },
				null,
				2
			);
		} catch (error) {
			// A half-typed number, most likely. The form still shows the problem; the
			// JSON just cannot represent it yet.
			draft = error instanceof PayloadError ? `// ${error.message}` : String(error);
		}
	}

	// Runs whenever any field in the tree changes, because buildPayload reads the
	// whole tree and $state is deeply reactive.
	$effect(() => {
		void fields;
		render();
	});

	function responseShape(request: SchemaRequest): string {
		if (request.response.properties.length === 0) {
			return `${request.response.typeName} (no payload)`;
		}
		const shape = request.response.properties
			.map(
				(property) =>
					`${property.wireName}${property.optional ? '?' : ''}: ${property.typeName}${property.multiple ? '[]' : ''}`
			)
			.join(', ');
		return `${request.response.typeName} { ${shape} }`;
	}

	async function send(): Promise<void> {
		sending = true;
		result = null;
		try {
			// The text goes out exactly as typed: no parse, no re-encode, no size
			// check. Producing a frame the backend refuses is the point of this page,
			// and anything validated here could not be produced at all.
			const response = await getWsClient().sendRaw(draft);
			result = {
				ok: true,
				text:
					response === undefined
						? 'Sent. The frame carries no requestId, so nothing here can match its reply — see the traffic log.'
						: JSON.stringify(response, null, 2)
			};
		} catch (error) {
			result = { ok: false, text: error instanceof Error ? error.message : String(error) };
		} finally {
			sending = false;
			// The id is spent either way, but only the form's own draft is rewritten
			// with the next one: a hand-edited frame keeps the id typed into it, so it
			// can be sent again unchanged or reused on purpose.
			if (!edited) {
				requestId = null;
				render();
			}
		}
	}
</script>

<Column class="gap-(--sp-6)">
	<Row class="flex-wrap items-center gap-(--sp-5)">
		<span class="oi-label-md text-text-strong">Request</span>
		{#if protocol}
			<Select
				class="max-w-[24rem]"
				value={selectedType}
				onchange={(event) => choose(event.currentTarget.value)}
			>
				{#each requests as request (request.typeName)}
					<option value={request.typeName}>{request.typeName}</option>
				{/each}
			</Select>
		{:else if schemaState.status === 'failed'}
			<!-- The retry sits here rather than in the header or the Backend panel: this is
			     the control that is unusable, so it is where the way out belongs. -->
			<span role="alert" class="oi-body-sm text-text-danger">
				{schemaState.error ?? 'The backend did not answer with its contract.'}
			</span>
			<Button size="sm" onclick={() => void loadProtocolSchema()}>Retry</Button>
		{:else}
			<span class="oi-body-sm text-text-faint">reading the contract from the backend…</span>
		{/if}
	</Row>

	{#if protocol && requests.length === 0}
		<span class="oi-body-sm text-text-faint">This backend's contract declares no requests.</span>
	{/if}

	{#if selected && protocol}
		<span class="oi-body-sm text-text-faint">→ {responseShape(selected)}</span>

		<Column class="gap-(--sp-6) rounded-md border border-line-soft bg-surface-card p-(--pad-card)">
			{#each fields as field (field.property.name)}
				<PropertyField {field} schema={protocol} />
			{:else}
				<span class="oi-body-sm text-text-faint">This request carries no properties.</span>
			{/each}
		</Column>

		<Column class="gap-(--sp-4)">
			<Row class="items-center gap-(--sp-5)">
				<span class="oi-label-sm text-text-muted">Frame</span>
				<span class="oi-num-sm {tooLarge ? 'text-text-danger' : 'text-text-faint'}">
					{bytes} / {MAX_MESSAGE_BYTES} bytes
				</span>
				{#if edited}
					<span class="oi-body-sm text-text-faint">edited by hand</span>
					<Button size="sm" variant="ghost" onclick={() => ((edited = false), render())}>
						rebuild from form
					</Button>
				{/if}
			</Row>
			<textarea
				class="oi-scroll min-h-40 w-full rounded-sm border border-line-soft bg-surface-inset px-(--pad-control-x) py-(--pad-control-y) font-mono text-text-strong shadow-(--inset-well) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
				spellcheck="false"
				value={draft}
				oninput={(event) => ((edited = true), (draft = event.currentTarget.value))}></textarea>
			{#if tooLarge}
				<!-- Socket.cs reads a single 1 KiB buffer and throws on anything longer,
				     so the backend will drop the connection rather than answer. Sending it
				     anyway is allowed: that is a failure worth being able to stage. -->
				<span role="alert" class="oi-body-sm text-text-danger">
					Over the frame limit — the backend will close the connection.
				</span>
			{/if}
			<Row class="gap-(--sp-5)">
				<Button variant="primary" onclick={send} disabled={sending}>
					{sending ? 'sending…' : 'Send'}
				</Button>
			</Row>
		</Column>

		{#if result}
			<Column class="gap-(--sp-4)">
				<span class="oi-label-sm {result.ok ? 'text-text-muted' : 'text-text-danger'}">
					{result.ok ? 'Response' : 'Failed'}
				</span>
				<pre
					class="oi-scroll oi-body-sm max-h-80 overflow-auto rounded-sm border border-line-soft bg-surface-inset p-(--sp-6) font-mono text-text-body">{result.text}</pre>
			</Column>
		{/if}
	{/if}
</Column>
