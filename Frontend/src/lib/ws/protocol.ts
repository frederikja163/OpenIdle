// Mirrors the backend wire protocol (Backend/Dtos): JSON text frames where
// "$type" is the exact C# class name and must be the first property, property
// names are PascalCase, and requests carry a client-chosen numeric Id that the
// matching response echoes back. Server-push events have no Id.

export interface ProfileDto {
	Name: string;
	ProfileId: string;
}

export interface PongResponse {
	$type: 'PongResponse';
	Id: number | null;
}

export interface LoginAsTestUserResponse {
	$type: 'LoginAsTestUserResponse';
	Id: number | null;
}

export interface CreateProfileResponse {
	$type: 'CreateProfileResponse';
	Id: number | null;
}

export interface ListProfilesResponse {
	$type: 'ListProfilesResponse';
	Id: number | null;
	Profiles: ProfileDto[];
}

export interface SelectProfileResponse {
	$type: 'SelectProfileResponse';
	Id: number | null;
}

export interface ErrorResponse {
	$type: 'ErrorResponse';
	Id: number | null;
	Message: string;
}

export type ServerResponse =
	| PongResponse
	| LoginAsTestUserResponse
	| CreateProfileResponse
	| ListProfilesResponse
	| SelectProfileResponse;

// No concrete EventBase subclass exists in the backend yet; this models any
// future server-push message (no Id).
export interface ServerEvent {
	$type: string;
	[key: string]: unknown;
}

export type RequestMap = {
	PingRequest: { payload: Record<string, never>; response: PongResponse };
	LoginAsTestUserRequest: { payload: Record<string, never>; response: LoginAsTestUserResponse };
	CreateProfileRequest: { payload: { Name: string }; response: CreateProfileResponse };
	ListProfilesRequest: { payload: Record<string, never>; response: ListProfilesResponse };
	SelectProfileRequest: { payload: { ProfileId: string }; response: SelectProfileResponse };
};

export type RequestType = keyof RequestMap;

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
	const json = JSON.stringify({ $type: type, Id: id, ...payload });
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
	const message = parsed as { $type: string; Id?: number | null };
	if (message.$type === 'ErrorResponse') {
		return { kind: 'error', message: message as ErrorResponse };
	}
	if (typeof message.Id === 'number') {
		return { kind: 'response', id: message.Id, message: message as ServerResponse };
	}
	return { kind: 'event', message: message as ServerEvent };
}
