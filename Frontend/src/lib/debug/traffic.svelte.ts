import { getWsClient, type FrameDirection } from '$lib/ws/client';
import { classifyMessage } from '$lib/ws/protocol';

/*
 * The console's record of what crossed the socket.
 *
 * Fed from WsClient.onFrame, which sees raw text before anything classifies it,
 * so a frame the client itself could not understand still shows up here — which
 * is exactly the frame worth seeing.
 *
 * Not sessionState(): the log's whole value is surviving the thing it recorded.
 * A disconnect is a line in it, not a reason to empty it.
 */

/**
 * What a frame is, to this side's eyes. `unknown` is anything this side could
 * not classify — the frame most worth seeing, so it is never filterable.
 */
export type FrameKind = 'request' | 'response' | 'event' | 'unknown';

export interface Frame {
	id: number;
	direction: FrameDirection;
	/**
	 * What the frame was, to this side's eyes. `unknown` is anything here that
	 * did not parse as a known exchange — the frame most worth seeing, so the
	 * traffic filter never hides it.
	 */
	kind: FrameKind;
	/** Wall-clock, for reading against the server log. */
	time: string;
	/** The same instant as `time`, kept for measuring a round trip. */
	at: number;
	/** The message's `$type`, or null for something that did not parse as one. */
	type: string | null;
	/** Correlates a response to its request; absent on events. */
	requestId: number | null;
	/**
	 * An ErrorResponse's message. Lifted out of the payload so the row can show
	 * it without being expanded: a failure is what the reader came for, and
	 * hiding it one click deep makes the log slower to scan than the server log.
	 */
	error: string | null;
	/** The `id` of the frame on the other half of this exchange, once it lands. */
	pairedFrameId: number | null;
	/** Round trip in milliseconds, set on the response half only. */
	elapsedMs: number | null;
	/**
	 * Send-to-arrival milliseconds, set on events only: arrival on this
	 * machine's clock minus the backend's send stamp, so skew between the two
	 * clocks is part of the number and can even push it negative.
	 */
	travelMs: number | null;
	/**
	 * The backend's per-socket event sequence. Starts at 0, so a zero here is
	 * the connection's first event — not requestId's could-not-parse marker.
	 */
	eventId: number | null;
	raw: string;
	/** Pretty-printed, or the raw text when it is not JSON. */
	pretty: string;
	bytes: number;
}

/**
 * Old frames are dropped rather than kept for ever: the log is a live tail, and
 * an idle page left open on a chatty backend would otherwise grow without bound.
 */
const MAX_FRAMES = 500;

export const trafficState = $state({
	frames: [] as Frame[],
	paused: false
});

let nextFrameId = 1;
let unsubscribe: (() => void) | null = null;

/** Starts recording. Returns the teardown for the page's $effect. */
export function recordTraffic(): () => void {
	// The tap is registered once even across a hot reload, because the client is a
	// singleton whose handler set only ever grows: a second subscription would
	// double every line.
	unsubscribe ??= getWsClient().onFrame((direction, raw) => {
		if (trafficState.paused) {
			return;
		}
		const frame = describe(direction, raw);
		trafficState.frames.unshift(frame);
		pair(frame);
		if (trafficState.frames.length > MAX_FRAMES) {
			trafficState.frames.length = MAX_FRAMES;
		}
	});
	return () => {
		unsubscribe?.();
		unsubscribe = null;
	};
}

export function clearTraffic(): void {
	trafficState.frames = [];
}

/** The frame the given one answers or was answered by, if it is still in the log. */
export function partnerOf(frame: Frame): Frame | null {
	if (frame.pairedFrameId === null) {
		return null;
	}
	// Looked up rather than held as a reference, because the MAX_FRAMES cap can
	// drop one half of a pair while the other is still on screen.
	return trafficState.frames.find((candidate) => candidate.id === frame.pairedFrameId) ?? null;
}

/**
 * Links an arriving reply to the request it answers. Done here rather than in
 * the view because the pairing is a fact about the exchange, and the view would
 * have to rediscover it on every render.
 */
function pair(frame: Frame): void {
	// requestId 0 is what the backend echoes for a frame it could not read far
	// enough to find an id in, so it names no request and must not match one.
	if (frame.direction !== 'in' || frame.requestId === null || frame.requestId <= 0) {
		return;
	}
	// Newest first, so the first match is the most recent request with that id —
	// which is the right one when an id is deliberately reused.
	const request = trafficState.frames.find(
		(candidate) =>
			candidate.direction === 'out' &&
			candidate.requestId === frame.requestId &&
			candidate.pairedFrameId === null
	);
	if (!request) {
		return;
	}
	request.pairedFrameId = frame.id;
	frame.pairedFrameId = request.id;
	frame.elapsedMs = frame.at - request.at;
}

function kindOf(direction: FrameDirection, raw: string): FrameKind {
	if (direction === 'out') {
		return 'request';
	}
	// classifyMessage rather than describe()'s own parse, so the log agrees with
	// what WsClient dispatches — including its rule that a known response type
	// without a requestId is malformed, not an event. An ErrorResponse answers a
	// request, so it is a response; the row already carries its message.
	const classified = classifyMessage(raw);
	return classified.kind === 'error' ? 'response' : classified.kind;
}

function describe(direction: FrameDirection, raw: string): Frame {
	const now = new Date();
	const frame: Frame = {
		id: nextFrameId++,
		direction,
		kind: kindOf(direction, raw),
		time: now.toLocaleTimeString(),
		at: now.getTime(),
		type: null,
		requestId: null,
		error: null,
		pairedFrameId: null,
		elapsedMs: null,
		travelMs: null,
		eventId: null,
		raw,
		pretty: raw,
		// What the backend's 1 KiB limit is measured in — the frame's UTF-8 length,
		// not its character count.
		bytes: new TextEncoder().encode(raw).length
	};
	try {
		const parsed: unknown = JSON.parse(raw);
		if (typeof parsed === 'object' && parsed !== null) {
			const message = parsed as {
				$type?: unknown;
				requestId?: unknown;
				message?: unknown;
				eventId?: unknown;
				timestamp?: unknown;
			};
			frame.type = typeof message.$type === 'string' ? message.$type : null;
			frame.requestId = typeof message.requestId === 'number' ? message.requestId : null;
			frame.error =
				message.$type === 'ErrorResponse' && typeof message.message === 'string'
					? message.message
					: null;
			// Only an event's timestamp is the backend's send stamp. The same key in
			// a hand-typed request is ordinary payload, and a travel time computed
			// from it would be nonsense.
			if (frame.kind === 'event') {
				frame.eventId = typeof message.eventId === 'number' ? message.eventId : null;
				frame.travelMs =
					typeof message.timestamp === 'number' ? frame.at - message.timestamp : null;
			}
		}
		frame.pretty = JSON.stringify(parsed, null, 2);
	} catch {
		// Left as the raw text: an unparseable frame is a finding, not an error.
	}
	return frame;
}
