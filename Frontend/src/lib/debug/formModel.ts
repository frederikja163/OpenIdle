import type { ProtocolSchema, SchemaProperty } from './schema';

/*
 * The editable shape behind the generated request form, and the payload it
 * builds.
 *
 * Every value is held as text, whatever the property's type, and converted only
 * when the frame is built. That is deliberate: a number field mid-edit is
 * legitimately "-" or "1.", and a model that stored numbers would have to either
 * reject those keystrokes or invent a value for them. It also means one control
 * per kind can round-trip through the JSON editor without a second representation.
 */

export class PayloadError extends Error {}

/** One element of a `multiple="true"` property, or the single value of a plain one. */
export type EntryNode = { kind: 'scalar'; value: string } | { kind: 'object'; fields: FieldNode[] };

export interface FieldNode {
	property: SchemaProperty;
	/**
	 * Only meaningful for an optional property. Unchecked omits the key from the
	 * frame entirely rather than sending a null, which is what `optional` means on
	 * the wire: the C# property simply keeps its default.
	 */
	include: boolean;
	/** One entry for a plain property; zero or more for an array. */
	entries: EntryNode[];
}

function emptyEntry(property: SchemaProperty, schema: ProtocolSchema): EntryNode {
	if (property.kind === 'dto') {
		const dto = schema.dtos[property.typeName];
		if (!dto) {
			throw new PayloadError(`No DTO named ${property.typeName} is declared.`);
		}
		// No cycle guard: a property can only name a type declared above it in
		// types.xml, so a DTO cannot reach itself.
		return { kind: 'object', fields: createFields(dto.properties, schema) };
	}
	if (property.kind === 'enum') {
		// Every enum has None as its first member, so there is always something to
		// start on and no empty state to represent.
		return { kind: 'scalar', value: schema.enums[property.typeName]?.values[0] ?? 'None' };
	}
	return { kind: 'scalar', value: '' };
}

export function createField(property: SchemaProperty, schema: ProtocolSchema): FieldNode {
	return {
		property,
		include: !property.optional,
		// An array starts empty — an unasked-for first element is a value the caller
		// never chose to send.
		entries: property.multiple ? [] : [emptyEntry(property, schema)]
	};
}

export function createFields(properties: SchemaProperty[], schema: ProtocolSchema): FieldNode[] {
	return properties.map((property) => createField(property, schema));
}

export function addEntry(field: FieldNode, schema: ProtocolSchema): void {
	field.entries.push(emptyEntry(field.property, schema));
}

/** Builds the JSON body of the frame. `$type` and `requestId` are the client's. */
export function buildPayload(fields: FieldNode[]): Record<string, unknown> {
	const payload: Record<string, unknown> = {};
	for (const field of fields) {
		if (field.property.optional && !field.include) {
			continue;
		}
		payload[field.property.wireName] = field.property.multiple
			? field.entries.map((entry) => entryValue(field.property, entry))
			: entryValue(field.property, field.entries[0]);
	}
	return payload;
}

function entryValue(property: SchemaProperty, entry: EntryNode | undefined): unknown {
	if (!entry) {
		throw new PayloadError(`${property.name} has no value.`);
	}
	if (entry.kind === 'object') {
		return buildPayload(entry.fields);
	}
	switch (property.kind) {
		case 'int':
		case 'long':
		case 'float': {
			// An empty number field means zero rather than an error: it is the value
			// the backend's int/float would have defaulted to anyway.
			if (entry.value.trim() === '') {
				return 0;
			}
			const parsed = Number(entry.value);
			if (!Number.isFinite(parsed)) {
				throw new PayloadError(`${property.name} is not a number: '${entry.value}'`);
			}
			if (property.kind !== 'float' && !Number.isInteger(parsed)) {
				throw new PayloadError(`${property.name} must be a whole number, got ${parsed}.`);
			}
			return parsed;
		}
		default:
			// string, Guid, UserId, ProfileId and enums are all strings on the wire —
			// enums because the backend installs a JsonStringEnumConverter.
			return entry.value;
	}
}
