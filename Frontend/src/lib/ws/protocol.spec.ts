import { describe, expect, it } from 'vitest';
import { classifyMessage, encodeRequest, MAX_MESSAGE_BYTES } from './protocol';

describe('encodeRequest', () => {
	it('emits $type as the first property', () => {
		const json = encodeRequest('PingRequest', 1, {});
		expect(json.startsWith('{"$type":"PingRequest"')).toBe(true);
	});

	it('includes the request id and PascalCase payload', () => {
		const json = encodeRequest('CreateProfileRequest', 7, { Name: 'Alice' });
		expect(JSON.parse(json)).toEqual({
			$type: 'CreateProfileRequest',
			Id: 7,
			Name: 'Alice'
		});
	});

	it('throws when the encoded frame exceeds the byte limit', () => {
		// 'æ' is 2 bytes in UTF-8, so 600 of them fit the char budget but not
		// the byte budget — proving the limit counts bytes.
		const name = 'æ'.repeat(600);
		expect(name.length).toBeLessThan(MAX_MESSAGE_BYTES);
		expect(() => encodeRequest('CreateProfileRequest', 1, { Name: name })).toThrow(/frame limit/);
	});
});

describe('classifyMessage', () => {
	it('classifies a message with a numeric Id as a response', () => {
		const classified = classifyMessage('{"$type":"LoginAsTestUserResponse","Id":3}');
		expect(classified).toEqual({
			kind: 'response',
			id: 3,
			message: { $type: 'LoginAsTestUserResponse', Id: 3 }
		});
	});

	it('classifies ErrorResponse as an error even though its Id is null', () => {
		const classified = classifyMessage('{"$type":"ErrorResponse","Id":null,"Message":"boom"}');
		expect(classified).toEqual({
			kind: 'error',
			message: { $type: 'ErrorResponse', Id: null, Message: 'boom' }
		});
	});

	it('classifies an Id-less message as a server event', () => {
		const classified = classifyMessage('{"$type":"SomeFutureEvent","Value":1}');
		expect(classified).toEqual({
			kind: 'event',
			message: { $type: 'SomeFutureEvent', Value: 1 }
		});
	});

	it('classifies a known response type with a null Id as unknown, not an event', () => {
		const raw = '{"$type":"LoginAsTestUserResponse","Id":null}';
		expect(classifyMessage(raw)).toEqual({ kind: 'unknown', raw });
	});

	it('classifies malformed or untyped payloads as unknown', () => {
		expect(classifyMessage('not json')).toEqual({ kind: 'unknown', raw: 'not json' });
		expect(classifyMessage('{"Id":1}')).toEqual({ kind: 'unknown', raw: '{"Id":1}' });
	});
});
