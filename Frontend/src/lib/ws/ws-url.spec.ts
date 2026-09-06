import { describe, expect, it } from 'vitest';
import {
	apiUrlFromWsUrl,
	DEFAULT_WS_URL,
	selectApiUrl,
	selectWsUrl,
	versionUrl,
	type WsUrlInput
} from './ws-url';

const DEV_BACKEND = 'wss://api.dev.openidle.example/ws';
const LOCAL_BACKEND = 'ws://localhost:5066/ws';

/**
 * A deployed dev frontend: PUBLIC_WS_URL points at the dev backend and the
 * build opted into overrides. Every case below varies one thing from here, so
 * what each test is actually about stays visible.
 */
function input(overrides: Partial<WsUrlInput> = {}): WsUrlInput {
	return {
		configured: DEV_BACKEND,
		overrideEnabled: true,
		storedOverride: null,
		requestedOverride: null,
		dev: false,
		...overrides
	};
}

describe('selectWsUrl', () => {
	it('uses the configured endpoint when nothing overrides it', () => {
		const resolution = selectWsUrl(input());

		expect(resolution).toMatchObject({
			url: DEV_BACKEND,
			source: 'configured',
			override: null,
			warnings: []
		});
	});

	it('falls back to the local backend only in development', () => {
		const resolution = selectWsUrl(input({ configured: undefined, dev: true }));

		expect(resolution).toMatchObject({ url: DEFAULT_WS_URL, source: 'dev-default' });
	});

	it('refuses to guess an endpoint outside development', () => {
		expect(() => selectWsUrl(input({ configured: undefined, dev: false }))).toThrow(
			'PUBLIC_WS_URL must be configured outside development'
		);
	});

	it('prefers an override supplied on this navigation', () => {
		const resolution = selectWsUrl(input({ requestedOverride: LOCAL_BACKEND }));

		expect(resolution).toMatchObject({ url: LOCAL_BACKEND, source: 'query-override' });
	});

	it('stores an override so it survives the next reload', () => {
		const resolution = selectWsUrl(input({ requestedOverride: LOCAL_BACKEND }));

		expect(resolution.override).toBe(LOCAL_BACKEND);
	});

	it('honours an override stored by an earlier visit', () => {
		const resolution = selectWsUrl(input({ storedOverride: LOCAL_BACKEND }));

		expect(resolution).toMatchObject({
			url: LOCAL_BACKEND,
			source: 'stored-override',
			override: LOCAL_BACKEND
		});
	});

	it('lets a new override replace a stored one', () => {
		const other = 'ws://localhost:6000/ws';
		const resolution = selectWsUrl(
			input({ storedOverride: LOCAL_BACKEND, requestedOverride: other })
		);

		expect(resolution).toMatchObject({ url: other, override: other });
	});

	it.each([
		['an empty parameter', ''],
		['the reset keyword', 'reset']
	])('clears a stored override on %s', (_label, parameter) => {
		const resolution = selectWsUrl(
			input({ storedOverride: LOCAL_BACKEND, requestedOverride: parameter })
		);

		expect(resolution).toMatchObject({
			url: DEV_BACKEND,
			source: 'configured',
			override: null,
			warnings: []
		});
	});

	it.each([
		['a plain word', 'not-a-url'],
		['an http URL', 'http://localhost:5066/ws'],
		['an https URL', 'https://api.openidle.example/ws']
	])('ignores %s and warns rather than failing', (_label, parameter) => {
		const resolution = selectWsUrl(input({ requestedOverride: parameter }));

		expect(resolution.url).toBe(DEV_BACKEND);
		expect(resolution.warnings).toHaveLength(1);
		expect(resolution.warnings[0]).toContain(parameter);
	});

	// A typo in the address bar should cost the warning above and nothing else.
	it('leaves a stored override in force when the parameter is malformed', () => {
		const resolution = selectWsUrl(
			input({ storedOverride: LOCAL_BACKEND, requestedOverride: 'not-a-url' })
		);

		expect(resolution).toMatchObject({ url: LOCAL_BACKEND, source: 'stored-override' });
		expect(resolution.warnings).toHaveLength(1);
	});

	it('discards a stored override that is no longer valid', () => {
		const resolution = selectWsUrl(input({ storedOverride: 'http://localhost:5066/ws' }));

		expect(resolution).toMatchObject({ url: DEV_BACKEND, source: 'configured', override: null });
		expect(resolution.warnings).toHaveLength(1);
	});

	// The point of the flag: production ships the same bundle, so the override
	// has to be inert there rather than merely undocumented.
	it('ignores every override when the build did not opt in', () => {
		const resolution = selectWsUrl(
			input({
				overrideEnabled: false,
				storedOverride: LOCAL_BACKEND,
				requestedOverride: LOCAL_BACKEND
			})
		);

		expect(resolution).toMatchObject({
			url: DEV_BACKEND,
			source: 'configured',
			override: null,
			warnings: []
		});
	});

	it('accepts a wss override', () => {
		const secure = 'wss://tunnel.example/ws';
		const resolution = selectWsUrl(input({ requestedOverride: secure }));

		expect(resolution).toMatchObject({ url: secure, source: 'query-override' });
	});

	it('trims surrounding whitespace, which a copied address often carries', () => {
		const resolution = selectWsUrl(input({ requestedOverride: `  ${LOCAL_BACKEND}  ` }));

		expect(resolution.url).toBe(LOCAL_BACKEND);
	});
});

describe('selectApiUrl', () => {
	const DEV_API = 'https://api.dev.openidle.example';

	it('uses the configured API URL for the configured backend', () => {
		const resolution = selectApiUrl({
			configured: DEV_API,
			wsUrl: DEV_BACKEND,
			wsOverridden: false
		});

		expect(resolution).toMatchObject({ url: DEV_API, source: 'configured', warnings: [] });
	});

	it('normalises the configured value, so a trailing slash cannot double up', () => {
		const resolution = selectApiUrl({
			configured: ` ${DEV_API}/ `,
			wsUrl: DEV_BACKEND,
			wsOverridden: false
		});

		expect(resolution.url).toBe(DEV_API);
		expect(versionUrl(resolution.url)).toBe(`${DEV_API}/version`);
	});

	it('follows an overridden socket instead of the configured API', () => {
		const resolution = selectApiUrl({
			configured: DEV_API,
			wsUrl: LOCAL_BACKEND,
			wsOverridden: true
		});

		expect(resolution).toMatchObject({ url: 'http://localhost:5066', source: 'derived' });
	});

	it('derives from the socket when nothing is configured', () => {
		const resolution = selectApiUrl({
			configured: undefined,
			wsUrl: DEV_BACKEND,
			wsOverridden: false
		});

		expect(resolution).toMatchObject({
			url: 'https://api.dev.openidle.example',
			source: 'derived'
		});
	});

	it('warns about and ignores a configured value that is not an http(s) URL', () => {
		for (const configured of ['wss://api.dev.openidle.example', 'not a url']) {
			const resolution = selectApiUrl({ configured, wsUrl: DEV_BACKEND, wsOverridden: false });

			expect(resolution.source).toBe('derived');
			expect(resolution.warnings).toEqual([expect.stringContaining(configured)]);
		}
	});
});

describe('apiUrlFromWsUrl', () => {
	it('maps a ws URL to the http base on the same host', () => {
		expect(apiUrlFromWsUrl('ws://localhost:5066/ws')).toBe('http://localhost:5066');
	});

	it('maps a wss URL to an https base', () => {
		expect(apiUrlFromWsUrl('wss://api.openidle.example/ws')).toBe('https://api.openidle.example');
	});

	it('keeps a path prefix the backend is mounted under', () => {
		expect(apiUrlFromWsUrl('wss://openidle.example/api/ws')).toBe('https://openidle.example/api');
		expect(apiUrlFromWsUrl('wss://openidle.example/api/ws/')).toBe('https://openidle.example/api');
	});

	it('drops the query and hash the ws URL carried', () => {
		expect(apiUrlFromWsUrl('ws://localhost:5066/ws?x=1#frag')).toBe('http://localhost:5066');
	});
});

describe('versionUrl', () => {
	it('appends /version to the API base', () => {
		expect(versionUrl('http://localhost:5066')).toBe('http://localhost:5066/version');
		expect(versionUrl('https://openidle.example/api')).toBe('https://openidle.example/api/version');
	});

	it('tolerates a base that already ends in a slash', () => {
		expect(versionUrl('http://localhost:5066/')).toBe('http://localhost:5066/version');
	});
});
