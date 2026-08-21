// The hand-written half of the wire protocol. Every DTO, request, response,
// event and enum comes from types.xml via ./dto.generated.ts and is re-exported
// here, so `$lib/ws/protocol` stays the single import for the whole protocol.
// What lives in this file is what a generator cannot produce: the frame size
// limit, encoding, and the classification of an incoming frame.
//
// Frames are JSON text where "$type" is the exact C# class name and must be the
// first property. Every other property is camelCase: the generator stamps an
// explicit [JsonPropertyName] on each one, and SocketJsonSerializer sets
// PropertyNamingPolicy = CamelCase on top of that. Requests carry a
// client-chosen `requestId` that the matching response echoes back. Server-push
// events carry neither id — the backend never assigns EventId — and that
// absence is what classifyMessage uses to tell an event from a response.

import {
	RESPONSE_TYPES,
	type ErrorResponse,
	type RequestMap,
	type RequestType,
	type ServerResponse
} from './dto.generated';

export * from './dto.generated';

/**
 * Any server-push message. Deliberately open rather than the generated
 * `EventBase`: the client dispatches on `$type` and has to be able to hand
 * `onAnyEvent` a message this build has never heard of.
 */
export interface ServerEvent {
	$type: string;
	[key: string]: unknown;
}

/*
 * The backend names events `<Noun>ChangedEvent` and puts the full replacement
 * state in them rather than a delta, so a handler can overwrite its slice
 * without reconciling.
 *
 * TODO: ProfilesChangedEvent arrives *before* the response to the request that
 * caused it, which makes createProfile's refetch redundant.
 */

// The backend reads a single 1 KiB frame and rejects anything larger.
export const MAX_MESSAGE_BYTES = 1024;

export function encodeRequest<K extends RequestType>(
	type: K,
	id: number,
	payload: RequestMap[K]['payload']
): string {
	// $type is inserted first on purpose: System.Text.Json requires the
	// discriminator to be the first property, and JSON.stringify preserves
	// insertion order.
	const json = JSON.stringify({ $type: type, requestId: id, ...payload });
	if (new TextEncoder().encode(json).length > MAX_MESSAGE_BYTES) {
		throw new Error(`Encoded ${type} exceeds the backend's ${MAX_MESSAGE_BYTES}-byte frame limit`);
	}
	return json;
}

export type Classified =
	| { kind: 'response'; id: number; message: ServerResponse }
	| { kind: 'error'; message: ErrorResponse }
	| { kind: 'event'; message: ServerEvent }
	| { kind: 'unknown'; raw: string };

export function classifyMessage(raw: string): Classified {
	let parsed: unknown;
	try {
		parsed = JSON.parse(raw);
	} catch {
		return { kind: 'unknown', raw };
	}
	if (typeof parsed !== 'object' || parsed === null || !('$type' in parsed)) {
		return { kind: 'unknown', raw };
	}
	const message = parsed as { $type: string; requestId?: number | null };
	if (message.$type === 'ErrorResponse') {
		return { kind: 'error', message: message as unknown as ErrorResponse };
	}
	if (typeof message.requestId === 'number') {
		return { kind: 'response', id: message.requestId, message: message as ServerResponse };
	}
	// A known response type without a numeric requestId is a malformed response,
	// not a server event: genuine events carry their own $type and never appear
	// in RESPONSE_TYPES, which the generator derives from the same requests the
	// ServerResponse union is built from.
	if (RESPONSE_TYPES.has(message.$type)) {
		return { kind: 'unknown', raw };
	}
	return { kind: 'event', message: message as ServerEvent };
}
