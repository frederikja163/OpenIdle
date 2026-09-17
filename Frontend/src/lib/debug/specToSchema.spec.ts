import { describe, expect, it } from 'vitest';
import { SchemaError, toProtocolSchema } from './specToSchema';

/*
 * The mapping from the hosted contract to the console's model.
 *
 * These cases are the reason the mapping may live in the browser at all: they are
 * what notices when the generator's naming stops being what is restated here.
 */

/**
 * The contract as `GET /schema` carries it, small enough to read and wide enough
 * to exercise every rule once. A factory so a case can bend one field without
 * the next case inheriting it.
 */
function contract() {
	return {
		Enums: [
			{ Name: 'SkillId', Values: [{ Name: 'None' }, { Name: 'Mining' }] },
			{ Name: 'ItemId', Values: [{ Name: 'None' }, { Name: 'Tin' }] }
		],
		Dtos: [
			{
				Name: 'SettingDto',
				Properties: [
					{ Name: 'Key', Type: 'String', Multiple: false, Optional: false },
					{ Name: 'Value', Type: 'String', Multiple: false, Optional: true }
				]
			},
			{
				Name: 'ProfileDto',
				Properties: [
					{ Name: 'ProfileId', Type: 'Guid', Multiple: false, Optional: false },
					{ Name: 'CreationTime', Type: 'TimeStamp', Multiple: false, Optional: false },
					{ Name: 'TotalLevel', Type: 'Int', Multiple: false, Optional: false },
					{ Name: 'Settings', Type: 'SettingDto', Multiple: true, Optional: true },
					{ Name: 'ActivityId', Type: 'SkillId', Multiple: false, Optional: false }
				]
			}
		],
		Requests: [
			{
				Name: 'GetItemsRequest',
				Properties: [{ Name: 'ItemIds', Type: 'ItemId', Multiple: true, Optional: false }],
				Responses: [
					{
						Name: 'GetItemsResponse',
						Properties: [{ Name: 'Items', Type: 'ItemId', Multiple: true, Optional: false }]
					}
				]
			},
			{
				Name: 'StopActivityRequest',
				Properties: [],
				// An anonymous response, which types.xml allows and the emitters name
				// after the request.
				Responses: [{ Name: null, Properties: [] }]
			}
		],
		Events: [
			{
				Name: 'ProfilesChangedEvent',
				Properties: [{ Name: 'Profiles', Type: 'ProfileDto', Multiple: true, Optional: false }]
			}
		],
		// Everything below is in the payload and has no part in a request form.
		Responses: [
			{
				Name: 'ErrorResponse',
				Properties: [{ Name: 'Message', Type: 'String', Multiple: false, Optional: false }]
			}
		],
		Items: [{ Name: 'Tin', Tags: [] }],
		Skills: [{ Name: 'Mining', Slots: [] }],
		DropTables: [],
		Activities: []
	};
}

describe('toProtocolSchema', () => {
	it('maps the hosted contract onto the console model', () => {
		expect(toProtocolSchema(contract())).toEqual({
			enums: {
				SkillId: { typeName: 'SkillId', values: ['None', 'Mining'] },
				ItemId: { typeName: 'ItemId', values: ['None', 'Tin'] }
			},
			dtos: {
				SettingDto: {
					typeName: 'SettingDto',
					properties: [
						{
							name: 'Key',
							wireName: 'key',
							kind: 'string',
							typeName: 'String',
							multiple: false,
							optional: false
						},
						{
							name: 'Value',
							wireName: 'value',
							kind: 'string',
							typeName: 'String',
							multiple: false,
							optional: true
						}
					]
				},
				ProfileDto: {
					typeName: 'ProfileDto',
					properties: [
						{
							name: 'ProfileId',
							wireName: 'profileId',
							kind: 'guid',
							typeName: 'Guid',
							multiple: false,
							optional: false
						},
						{
							name: 'CreationTime',
							wireName: 'creationTime',
							kind: 'long',
							typeName: 'TimeStamp',
							multiple: false,
							optional: false
						},
						{
							name: 'TotalLevel',
							wireName: 'totalLevel',
							kind: 'int',
							typeName: 'Int',
							multiple: false,
							optional: false
						},
						{
							name: 'Settings',
							wireName: 'settings',
							kind: 'dto',
							typeName: 'SettingDto',
							multiple: true,
							optional: true
						},
						{
							name: 'ActivityId',
							wireName: 'activityId',
							kind: 'enum',
							typeName: 'SkillId',
							multiple: false,
							optional: false
						}
					]
				}
			},
			requests: [
				{
					typeName: 'GetItemsRequest',
					properties: [
						{
							name: 'ItemIds',
							wireName: 'itemIds',
							kind: 'enum',
							typeName: 'ItemId',
							multiple: true,
							optional: false
						}
					],
					response: {
						typeName: 'GetItemsResponse',
						properties: [
							{
								name: 'Items',
								wireName: 'items',
								kind: 'enum',
								typeName: 'ItemId',
								multiple: true,
								optional: false
							}
						]
					}
				},
				{
					typeName: 'StopActivityRequest',
					properties: [],
					response: { typeName: 'StopActivityRequestResponse', properties: [] }
				}
			],
			events: [
				{
					typeName: 'ProfilesChangedEvent',
					properties: [
						{
							name: 'Profiles',
							wireName: 'profiles',
							kind: 'dto',
							typeName: 'ProfileDto',
							multiple: true,
							optional: false
						}
					]
				}
			]
		});
	});

	it('matches built-in type tokens however they are cased, and keeps the token as the label', () => {
		const raw = contract();
		raw.Requests[0].Properties = [
			{ Name: 'A', Type: 'string', Multiple: false, Optional: false },
			{ Name: 'B', Type: 'INT', Multiple: false, Optional: false },
			{ Name: 'C', Type: 'Float', Multiple: false, Optional: false },
			{ Name: 'D', Type: 'guid', Multiple: false, Optional: false },
			{ Name: 'E', Type: 'UserId', Multiple: false, Optional: false },
			{ Name: 'F', Type: 'ProfileId', Multiple: false, Optional: false },
			{ Name: 'G', Type: 'timestamp', Multiple: false, Optional: false }
		];

		const properties = toProtocolSchema(raw).requests[0].properties;

		expect(properties.map((property) => property.kind)).toEqual([
			'string',
			'int',
			'float',
			'guid',
			'userId',
			'profileId',
			// The wire primitive, because that is what picks the control; the semantic
			// token stays in typeName, which is what the field is labelled with.
			'long'
		]);
		expect(properties.map((property) => property.typeName)).toEqual([
			'string',
			'INT',
			'Float',
			'guid',
			'UserId',
			'ProfileId',
			'timestamp'
		]);
	});

	it('lowers only the first letter of a wire name', () => {
		const raw = contract();
		raw.Requests[0].Properties = [
			{ Name: 'ProfileId', Type: 'Guid', Multiple: false, Optional: false },
			{ Name: 'ItemIds', Type: 'ItemId', Multiple: true, Optional: false }
		];

		expect(toProtocolSchema(raw).requests[0].properties.map((p) => p.wireName)).toEqual([
			'profileId',
			'itemIds'
		]);
	});

	it('resolves a type declared after the property that names it', () => {
		const raw = contract();
		// types.xml requires a type to be declared above its use, so this cannot
		// arrive from a valid contract — but resolving in a second pass is what makes
		// that the contract's rule rather than this mapping's.
		raw.Dtos[0].Properties = [
			{ Name: 'Profile', Type: 'ProfileDto', Multiple: false, Optional: false }
		];

		expect(toProtocolSchema(raw).dtos.SettingDto.properties[0].kind).toBe('dto');
	});

	it('takes nothing from the game data or the responses declared outside a request', () => {
		const schema = toProtocolSchema(contract());

		expect(Object.keys(schema)).toEqual(['enums', 'dtos', 'requests', 'events']);
		expect(Object.keys(schema.dtos)).toEqual(['SettingDto', 'ProfileDto']);
	});

	it('reads a contract that declares no events', () => {
		const raw: Record<string, unknown> = contract();
		delete raw.Events;

		expect(toProtocolSchema(raw).events).toEqual([]);
	});

	it('refuses a property whose type the contract does not declare', () => {
		const raw = contract();
		raw.Dtos[1].Properties[0].Type = 'QuestDto';

		expect(() => toProtocolSchema(raw)).toThrow(
			new SchemaError(
				"ProfileDto.ProfileId has type 'QuestDto', which the contract does not declare."
			)
		);
	});

	it('refuses a request that is not answered by exactly one response', () => {
		const none = contract();
		none.Requests[1].Responses = [];
		expect(() => toProtocolSchema(none)).toThrow(
			/StopActivityRequest must declare exactly one response/
		);

		const two = contract();
		two.Requests[1].Responses = [
			{ Name: 'A', Properties: [] },
			{ Name: 'B', Properties: [] }
		];
		expect(() => toProtocolSchema(two)).toThrow(SchemaError);
	});

	it('refuses a body that is not a contract at all', () => {
		// What a backend built before /schema existed answers with: a 404 page, or
		// nothing that parses as the contract.
		expect(() => toProtocolSchema(null)).toThrow(SchemaError);
		expect(() => toProtocolSchema('<!doctype html>')).toThrow(SchemaError);
		expect(() => toProtocolSchema([])).toThrow(SchemaError);
		expect(() => toProtocolSchema({ ...contract(), Requests: 'none' })).toThrow(/not a list/);
		expect(() => toProtocolSchema({ ...contract(), Dtos: [{ Properties: [] }] })).toThrow(
			/no name/
		);
	});
});
