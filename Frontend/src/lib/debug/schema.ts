/*
 * The shape of the protocol contract, as the debug console consumes it.
 *
 * The data itself is generated from types.xml by Generators/Core/TsSchemaEmitter.cs — see
 * `bun run generate`. These interfaces are hand-written and the generated file imports them
 * to annotate its export, so the two are checked against each other by `bun run check`: an
 * emitter that stops matching this file fails the build rather than the page.
 *
 * The names and casing here are the generator's, not this file's invention:
 *  - `typeName` is the generated class name and the `$type` on the wire — already carrying
 *    its Dto / Request / Response / Event suffix;
 *  - `wireName` is the lower-camel JSON key, `name` the PascalCase one from types.xml;
 *  - an enum's `values` lead with `None`, which every enum gets whether types.xml lists it
 *    or not, and include the DropTableId / ActivityId members synthesised from
 *    <DropTable> / <Activity> elements.
 *
 * Enums travel as their member name, not an ordinal: the backend installs a
 * JsonStringEnumConverter (Backend/SocketJsonSerializer.cs).
 */

export type PropertyKind =
	| 'string'
	| 'int'
	| 'float'
	| 'guid'
	| 'userId'
	| 'profileId'
	/** Epoch milliseconds — a whole number on the wire, like `int`. */
	| 'timestamp'
	| 'enum'
	| 'dto';

export interface SchemaProperty {
	/** The PascalCase name from types.xml, e.g. `ProfileId`. */
	name: string;
	/** The JSON key on the wire, e.g. `profileId`. */
	wireName: string;
	kind: PropertyKind;
	/** For `enum`/`dto`, the generated type name (`ItemId`, `ProfileDto`); otherwise the XML token. */
	typeName: string;
	multiple: boolean;
	optional: boolean;
}

export interface SchemaObject {
	typeName: string;
	properties: SchemaProperty[];
}

export interface SchemaEnum {
	typeName: string;
	/** Member names as they appear on the wire, `None` first. */
	values: string[];
}

export interface SchemaRequest extends SchemaObject {
	response: SchemaObject;
}

/**
 * Records rather than Maps, because a generated file can write an object literal but not a
 * Map literal, and a conversion step would only exist to be kept in step.
 */
export interface ProtocolSchema {
	/** Keyed by generated name, e.g. `ItemId`. */
	enums: Record<string, SchemaEnum>;
	/** Keyed by generated name, e.g. `ProfileDto`. */
	dtos: Record<string, SchemaObject>;
	requests: SchemaRequest[];
	events: SchemaObject[];
}

export { PROTOCOL } from './protocol.generated';
