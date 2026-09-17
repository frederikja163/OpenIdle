import type {
	ProtocolSchema,
	PropertyKind,
	SchemaEnum,
	SchemaObject,
	SchemaProperty,
	SchemaRequest
} from './schema';

/*
 * Turns the contract the backend hosts at GET /schema into the shape the console
 * consumes.
 *
 * The hosted payload is the parsed contract as the generator holds it — the
 * literal XML tree, serialized: PascalCase element and attribute names, type
 * attributes still carrying the token they were written as, and a request's
 * response still a list. ProtocolSchema is what a form needs instead: a wire
 * name per property, a kind to pick a control by, and enums and DTOs addressable
 * by name.
 *
 * The rules applied here are the generator's own, and the reason they can be
 * restated in TypeScript at all is that PR #81 moved the naming into types.xml:
 * every type is written with its Dto / Request / Response / Event suffix
 * already, so the only casing rule left is Extensions.ToCamelCase — lowercase
 * the first letter. What remains is the type-token table below, which is
 * TsEmitterVisitor.GetPropertyType's table, and the enum-or-DTO split, which is
 * a lookup rather than a rule. When one of those does change, the tests beside
 * this file are what catch it.
 *
 * The contract is validated server-side (ValidationVisitor runs before the JSON
 * is emitted), so anything unresolvable here means this frontend is not talking
 * to a backend whose contract it understands. That fails the load with a message
 * naming what could not be read, rather than being coerced into something the
 * form would misrepresent.
 */

/** A contract that cannot be read as one. Carries the reason for the panel to show. */
export class SchemaError extends Error {}

/**
 * The tokens types.xml may name instead of a declared type, keyed in lower case
 * because the generator matches them case-insensitively — the keys are
 * ValidationVisitor.BuiltInTypes, the values TsEmitterVisitor's mapping of them.
 *
 * `TimeStamp` is epoch milliseconds on the wire, so it is a `long` to the form,
 * and nothing else produces that kind; the token itself survives in `typeName`,
 * which is what the field is labelled with.
 */
const BUILT_IN_KINDS: Record<string, PropertyKind> = {
	string: 'string',
	int: 'int',
	float: 'float',
	guid: 'guid',
	userid: 'userId',
	profileid: 'profileId',
	timestamp: 'long'
};

/** The contract as the hosted JSON spells it. */
interface SpecProperty {
	Name: string;
	Type: string;
	Multiple: boolean;
	Optional: boolean;
}

interface SpecObject {
	/** Null for an anonymous response, which is named after its request instead. */
	Name: string | null;
	Properties: SpecProperty[];
}

interface SpecRequest extends SpecObject {
	Responses: SpecObject[];
}

interface SpecEnum {
	Name: string;
	Values: { Name: string }[];
}

export function toProtocolSchema(body: unknown): ProtocolSchema {
	const root = asRecord(body, 'The schema endpoint');

	const enums: Record<string, SchemaEnum> = {};
	for (const spec of asArray<SpecEnum>(root.Enums, 'Enums')) {
		const typeName = asName(spec?.Name, 'An enum');
		enums[typeName] = {
			typeName,
			values: asArray<{ Name: string }>(spec.Values, `${typeName}.Values`).map((value) =>
				asName(value?.Name, `A value of ${typeName}`)
			)
		};
	}

	// Built before any property is mapped, because a property's kind is decided by
	// which of these two its type token is found in.
	const dtoSpecs = asArray<SpecObject>(root.Dtos, 'Dtos');
	const dtoNames = new Set(dtoSpecs.map((spec) => asName(spec?.Name, 'A DTO')));

	const resolve = (spec: SpecObject, fallbackName: string): SchemaObject => {
		const typeName = spec.Name ?? fallbackName;
		return {
			typeName,
			properties: asArray<SpecProperty>(spec.Properties, `${typeName}.Properties`).map((property) =>
				toProperty(property, typeName, enums, dtoNames)
			)
		};
	};

	const dtos: Record<string, SchemaObject> = {};
	for (const spec of dtoSpecs) {
		const dto = resolve(spec, '');
		dtos[dto.typeName] = dto;
	}

	const requests: SchemaRequest[] = asArray<SpecRequest>(root.Requests, 'Requests').map((spec) => {
		const typeName = asName(spec?.Name, 'A request');
		const responses = asArray<SpecObject>(spec.Responses, `${typeName}.Responses`);
		if (responses.length !== 1) {
			// The same rule ValidationVisitor enforces, restated because a contract
			// that broke it would otherwise reach the form as a request with no reply.
			throw new SchemaError(`${typeName} must declare exactly one response.`);
		}
		// An anonymous response is named after its request, which is the name the
		// emitters give it too — see TsEmitterVisitor.EmitResponseUnion.
		return {
			...resolve(spec, typeName),
			response: resolve(responses[0], `${typeName}Response`)
		};
	});

	const events = asArray<SpecObject>(root.Events, 'Events').map((spec) => {
		asName(spec?.Name, 'An event');
		return resolve(spec, '');
	});

	// The payload also carries the game data (items, skills, drop tables,
	// activities) and the responses declared outside a request. The console builds
	// request frames, so neither has a part to play here.
	return { enums, dtos, requests, events };
}

function toProperty(
	spec: SpecProperty,
	owner: string,
	enums: Record<string, SchemaEnum>,
	dtoNames: ReadonlySet<string>
): SchemaProperty {
	const name = asName(spec?.Name, `A property of ${owner}`);
	const type = asName(spec.Type, `${owner}.${name}`);
	return {
		name,
		wireName: toCamelCase(name),
		kind: kindOf(type, `${owner}.${name}`, enums, dtoNames),
		typeName: type,
		multiple: spec.Multiple === true,
		optional: spec.Optional === true
	};
}

function kindOf(
	type: string,
	where: string,
	enums: Record<string, SchemaEnum>,
	dtoNames: ReadonlySet<string>
): PropertyKind {
	const builtIn = BUILT_IN_KINDS[type.toLowerCase()];
	if (builtIn) {
		return builtIn;
	}
	if (type in enums) {
		return 'enum';
	}
	if (dtoNames.has(type)) {
		return 'dto';
	}
	throw new SchemaError(`${where} has type '${type}', which the contract does not declare.`);
}

/** Extensions.ToCamelCase: the generator's whole casing rule. */
function toCamelCase(name: string): string {
	return name[0] === name[0].toUpperCase() ? name[0].toLowerCase() + name.slice(1) : name;
}

function asRecord(value: unknown, what: string): Record<string, unknown> {
	if (value === null || typeof value !== 'object' || Array.isArray(value)) {
		throw new SchemaError(`${what} did not answer with a contract.`);
	}
	return value as Record<string, unknown>;
}

/**
 * A missing collection is an empty one — the serializer writes every list, but a
 * contract with no events is a legitimate contract and so is an older backend
 * that does not send one. A value that is present and not a list is not.
 */
function asArray<T>(value: unknown, what: string): T[] {
	if (value === undefined || value === null) {
		return [];
	}
	if (!Array.isArray(value)) {
		throw new SchemaError(`${what} is not a list.`);
	}
	return value as T[];
}

function asName(value: unknown, what: string): string {
	if (typeof value !== 'string' || value === '') {
		throw new SchemaError(`${what} has no name.`);
	}
	return value;
}
