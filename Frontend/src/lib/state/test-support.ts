import { vi } from 'vitest';

/*
 * The socket every store spec runs against. Shared rather than restated per
 * spec, because the shape is not arbitrary: it is whatever the stores happen to
 * reach for, and a client method added to one copy and forgotten in the others
 * fails three suites separately.
 *
 * Deliberately imports nothing from the stores themselves. A spec installs this
 * as the mock for `$lib/ws/client`, and anything this file imported would be
 * pulled in while that mock is still being built.
 */

/** Every request the stores send. Mocked per case with vitest's own helpers. */
export const request = vi.fn();

/**
 * The generation the stores read to decide whether a result still belongs to the
 * live connection. Bumping it is a socket dropping.
 */
export const connection = { generation: 0 };

// One object handed back on every call, like the real singleton: sessionRun
// compares the generation it captured against the one on the client it fetches
// later, so a fresh object per call would be testing a contract we do not ship.
const client = {
	request,
	get generation() {
		return connection.generation;
	},
	onClose: () => () => {},
	onStatus: () => () => {},
	onEvent: () => () => {},
	setResume: () => {},
	reopen: () => {}
};

/**
 * The module namespace to hand `vi.mock('$lib/ws/client', ...)`:
 *
 * ```ts
 * vi.mock('$lib/ws/client', async () => (await import('$lib/state/test-support')).clientModule);
 * ```
 */
export const clientModule = { getWsClient: () => client };

/** Puts the socket back to a fresh connection that has answered nothing. */
export function resetConnection(): void {
	request.mockReset();
	connection.generation = 0;
}
