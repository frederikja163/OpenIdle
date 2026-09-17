/*
 * The shape of the protocol contract, as the debug console consumes it.
 *
 * The data itself is fetched from the backend's `GET /schema`, which hosts the contract it
 * was built from, and mapped onto these interfaces by ./specToSchema — so the catalogue
 * describes the backend under test rather than the revision this bundle was built from.
 * That mapping restates a handful of the generator's naming rules in TypeScript; the
 * trade-off, and why it is now the cheaper side, is argued there.
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

/**
 * How a value is rendered and encoded, not what it means: a `Timestamp` in
 * types.xml arrives here as `long` (epoch milliseconds, a whole number on the
 * wire) and is labelled from `typeName`.
 */
export type PropertyKind =
	'string' | 'int' | 'long' | 'float' | 'guid' | 'userId' | 'profileId' | 'enum' | 'dto';

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

/** Records rather than Maps: a property names its type, and both of these are looked up by it. */
export interface ProtocolSchema {
	/** Keyed by generated name, e.g. `ItemId`. */
	enums: Record<string, SchemaEnum>;
	/** Keyed by generated name, e.g. `ProfileDto`. */
	dtos: Record<string, SchemaObject>;
	requests: SchemaRequest[];
	events: SchemaObject[];
}
