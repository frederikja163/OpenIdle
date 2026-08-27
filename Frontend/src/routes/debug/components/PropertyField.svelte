<script lang="ts">
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { addEntry, type EntryNode, type FieldNode } from '$lib/debug/formModel';
	import { PROTOCOL, type SchemaProperty } from '$lib/debug/schema';
	import { profilesState } from '$lib/state/profiles.svelte';
	import Select from './Select.svelte';
	import Self from './PropertyField.svelte';

	/*
	 * One row of the generated request form. Recursive, because a property may name
	 * a DTO whose own properties need the same treatment.
	 */
	interface Props {
		field: FieldNode;
	}

	let { field }: Props = $props();

	const property = $derived(field.property);

	/*
	 * Which properties get a dropdown of real values instead of a bare text box.
	 *
	 * `ProfileId` is a declared type in the contract, but nothing uses it yet —
	 * SelectProfileRequest types its profileId as a plain Guid — so the wire name
	 * is checked too. That is a heuristic and it is meant to be: guessing wrong
	 * costs nothing, because every dropdown keeps a "custom…" escape hatch.
	 */
	const optionSource = $derived(
		property.kind === 'profileId' || (property.kind === 'guid' && property.wireName === 'profileId')
			? 'profiles'
			: null
	);

	const profileOptions = $derived(
		profilesState.profiles.map((profile) => ({
			value: profile.profileId,
			label: `${profile.name} — ${profile.profileId}`
		}))
	);

	// The sentinel a dropdown selects to reveal the free-text box beside it. Not a
	// value any Guid could collide with.
	const CUSTOM = '__custom__';

	function isKnown(entry: EntryNode): boolean {
		return entry.kind === 'scalar' && profileOptions.some((option) => option.value === entry.value);
	}

	/*
	 * Adopts the first known profile the moment one is available.
	 *
	 * The dropdown exists so that nobody has to paste a Guid, but a field that
	 * starts empty shows "custom…" and an empty box — and sending that empty Guid
	 * gets back "Internal server error." from a deserializer that never reached the
	 * handler.
	 *
	 * Once, and only once: choosing "custom…" empties the value on purpose, and a
	 * seeding that kept watching for empties would fill it straight back in and
	 * make custom unreachable.
	 */
	let seeded = $state(false);
	$effect(() => {
		if (seeded || optionSource !== 'profiles' || profileOptions.length === 0) {
			return;
		}
		seeded = true;
		for (const entry of field.entries) {
			if (entry.kind === 'scalar' && entry.value === '') {
				entry.value = profileOptions[0].value;
			}
		}
	});

	function label(property: SchemaProperty): string {
		const parts = [property.typeName];
		if (property.multiple) {
			parts.push('[]');
		}
		return `${property.name}: ${parts.join('')}`;
	}
</script>

{#snippet scalar(entry: EntryNode & { kind: 'scalar' })}
	{#if property.kind === 'enum'}
		<Select bind:value={entry.value}>
			{#each PROTOCOL.enums[property.typeName]?.values ?? [] as member (member)}
				<option value={member}>{member}</option>
			{/each}
		</Select>
	{:else if optionSource === 'profiles'}
		<Column class="w-full gap-(--sp-3)">
			<Select
				value={isKnown(entry) ? entry.value : CUSTOM}
				onchange={(event) => {
					const chosen = event.currentTarget.value;
					// Choosing "custom…" clears the box rather than leaving the profile id
					// that was there, so the field reads as ready for typing.
					entry.value = chosen === CUSTOM ? '' : chosen;
				}}
			>
				{#if profileOptions.length === 0}
					<option value={CUSTOM}>no profiles loaded — use Session above</option>
				{/if}
				{#each profileOptions as option (option.value)}
					<option value={option.value}>{option.label}</option>
				{/each}
				<option value={CUSTOM}>custom…</option>
			</Select>
			{#if !isKnown(entry)}
				<Input bind:value={entry.value} placeholder="00000000-0000-0000-0000-000000000000" />
			{/if}
		</Column>
	{:else if property.kind === 'int' || property.kind === 'float'}
		<!-- Deliberately not bind:value: Svelte coerces a number input's binding to a
		     number, and half-typed text like "-" or "1." has no number to be. The
		     model keeps every value as text and converts once, when the frame is
		     built. -->
		<Input
			type="number"
			step={property.kind === 'int' ? 1 : 'any'}
			value={entry.value}
			oninput={(event) => (entry.value = event.currentTarget.value)}
			placeholder="0"
		/>
	{:else}
		<Input bind:value={entry.value} placeholder={property.typeName} />
	{/if}
{/snippet}

{#snippet body(entry: EntryNode)}
	{#if entry.kind === 'object'}
		<!-- A nested DTO: indented and ruled, so the shape of the payload is legible
		     in the form the way it is in the JSON beside it. -->
		<Column class="gap-(--sp-5) border-l border-line-soft pl-(--sp-6)">
			{#each entry.fields as nested (nested.property.name)}
				<Self field={nested} />
			{/each}
		</Column>
	{:else}
		{@render scalar(entry)}
	{/if}
{/snippet}

<Column class="gap-(--sp-4)">
	<Row class="items-center gap-(--sp-4)">
		<span class="oi-label-sm text-text-muted">{label(property)}</span>
		{#if property.optional}
			<!-- Unchecked omits the key from the frame, which is what `optional` means
			     on the wire: the backend property keeps its default. -->
			<label class="oi-body-sm flex items-center gap-(--sp-3) text-text-faint">
				<input type="checkbox" bind:checked={field.include} />
				include
			</label>
		{/if}
	</Row>

	{#if property.optional && !field.include}
		<span class="oi-body-sm text-text-faint">omitted</span>
	{:else if property.multiple}
		<Column class="gap-(--sp-4)">
			{#each field.entries as entry, index (index)}
				<Row class="items-start gap-(--sp-4)">
					<span class="oi-num-sm pt-1.5 text-text-faint">{index}</span>
					<Column class="grow gap-(--sp-4)">{@render body(entry)}</Column>
					<Button
						size="sm"
						variant="ghost"
						onclick={() => field.entries.splice(index, 1)}
						aria-label={`Remove ${property.name} ${index}`}
					>
						remove
					</Button>
				</Row>
			{:else}
				<span class="oi-body-sm text-text-faint">empty array</span>
			{/each}
			<Row>
				<Button size="sm" onclick={() => addEntry(field, PROTOCOL)}>add {property.name}</Button>
			</Row>
		</Column>
	{:else}
		{@render body(field.entries[0])}
	{/if}
</Column>
