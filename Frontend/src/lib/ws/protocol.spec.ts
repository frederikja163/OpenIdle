import { describe, expect, it } from 'vitest';
import { classifyMessage, encodeRequest, MAX_MESSAGE_BYTES, readRequestId } from './protocol';

describe('encodeRequest', () => {
	it('emits $type as the first property', () => {
		const json = encodeRequest('LoginAsTestUserRequest', 1, {});
		expect(json.startsWith('{"$type":"LoginAsTestUserRequest"')).toBe(true);
	});

	it('includes the request id and camelCase payload', () => {
		const json = encodeRequest('CreateProfileRequest', 7, { name: 'Alice' });
		expect(JSON.parse(json)).toEqual({
			$type: 'CreateProfileRequest',
			requestId: 7,
			name: 'Alice'
		});
	});

	it('throws when the encoded frame exceeds the byte limit', () => {
		// 'æ' is 2 bytes in UTF-8, so 600 of them fit the char budget but not
		// the byte budget — proving the limit counts bytes.
		const name = 'æ'.repeat(600);
		expect(name.length).toBeLessThan(MAX_MESSAGE_BYTES);
		expect(() => encodeRequest('CreateProfileRequest', 1, { name })).toThrow(/frame limit/);
	});
});

describe('classifyMessage', () => {
	it('classifies a message with a numeric requestId as a response', () => {
		const classified = classifyMessage('{"$type":"LoginAsTestUserResponse","requestId":3}');
		expect(classified).toEqual({
			kind: 'response',
			id: 3,
			message: { $type: 'LoginAsTestUserResponse', requestId: 3 }
		});
	});

	it('classifies ErrorResponse as an error whatever its requestId', () => {
		const classified = classifyMessage('{"$type":"ErrorResponse","requestId":0,"message":"boom"}');
		expect(classified).toEqual({
			kind: 'error',
			message: { $type: 'ErrorResponse', requestId: 0, message: 'boom' }
		});
	});

	it('classifies a requestId-less message as a server event', () => {
		const classified = classifyMessage('{"$type":"SomeFutureEvent","value":1}');
		expect(classified).toEqual({
			kind: 'event',
			message: { $type: 'SomeFutureEvent', value: 1 }
		});
	});

	it('classifies a known response type with no requestId as unknown, not an event', () => {
		// What the backend sends when it never read an id off the request:
		// DefaultIgnoreCondition.WhenWritingNull drops the null rather than
		// echoing it, and there is no request such a frame could answer.
		const raw = '{"$type":"LoginAsTestUserResponse"}';
		expect(classifyMessage(raw)).toEqual({ kind: 'unknown', raw });
	});

	it('classifies malformed or untyped payloads as unknown', () => {
		expect(classifyMessage('not json')).toEqual({ kind: 'unknown', raw: 'not json' });
		expect(classifyMessage('{"requestId":1}')).toEqual({ kind: 'unknown', raw: '{"requestId":1}' });
	});
});

describe('readRequestId', () => {
	it('reads the id out of a frame', () => {
		expect(readRequestId('{"$type":"LoginAsTestUserRequest","requestId":7}')).toBe(7);
	});

	it('returns null for a frame with no id, a non-numeric one, or no JSON at all', () => {
		// The console sends whatever is typed, so all three reach this.
		expect(readRequestId('{"$type":"LoginAsTestUserRequest"}')).toBeNull();
		expect(readRequestId('{"requestId":"7"}')).toBeNull();
		expect(readRequestId('{ half a frame')).toBeNull();
		expect(readRequestId('42')).toBeNull();
	});
});
