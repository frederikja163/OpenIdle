import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// The tap recordTraffic() registers, captured so deliver() can speak to it the
// way the real WsClient does when a frame crosses the socket.
const { handlers } = vi.hoisted(() => ({
	handlers: [] as ((direction: 'in' | 'out', raw: string) => void)[]
}));

vi.mock('$lib/ws/client', () => ({
	getWsClient: () => ({
		onFrame: (handler: (direction: 'in' | 'out', raw: string) => void) => {
			handlers.push(handler);
			return () => {};
		}
	})
}));

const { clearTraffic, partnerOf, recordTraffic, trafficState } =
	await import('$lib/debug/traffic.svelte');

function deliver(direction: 'in' | 'out', raw: string): void {
	for (const handler of handlers) {
		handler(direction, raw);
	}
}

let stop: () => void;

beforeEach(() => {
	handlers.length = 0;
	clearTraffic();
	stop = recordTraffic();
});

afterEach(() => {
	stop();
});

describe('frame kinds', () => {
	it('marks an outbound frame a request', () => {
		deliver('out', '{"$type":"PingRequest","requestId":1}');

		expect(trafficState.frames[0].kind).toBe('request');
	});

	it('marks an inbound reply a response, paired to its request', () => {
		deliver('out', '{"$type":"PingRequest","requestId":1}');
		deliver('in', '{"$type":"PongResponse","requestId":1}');

		const reply = trafficState.frames[0];
		expect(reply.kind).toBe('response');
		expect(reply.elapsedMs).not.toBeNull();
		expect(partnerOf(reply)).toBe(trafficState.frames[1]);
	});

	it('marks an ErrorResponse a response and keeps its message on the row', () => {
		deliver('in', '{"$type":"ErrorResponse","requestId":7,"message":"boom"}');

		const frame = trafficState.frames[0];
		expect(frame.kind).toBe('response');
		expect(frame.error).toBe('boom');
	});

	it('marks a server-push frame an event', () => {
		deliver('in', '{"$type":"ProfilesChangedEvent","profiles":[]}');

		expect(trafficState.frames[0].kind).toBe('event');
	});

	it('marks an unparseable frame unknown, keeping the raw text', () => {
		deliver('in', 'definitely not json');

		const frame = trafficState.frames[0];
		expect(frame.kind).toBe('unknown');
		expect(frame.pretty).toBe(frame.raw);
	});

	it('marks a known response type without a requestId unknown, not an event', () => {
		deliver('in', '{"$type":"PongResponse"}');

		expect(trafficState.frames[0].kind).toBe('unknown');
	});
});
