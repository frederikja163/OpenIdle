import { defineConfig } from '@playwright/test';

export default defineConfig({
	webServer: {
		command: 'bun run build && bun run preview',
		port: 4173,
		env: {
			// Every test stubs the socket with routeWebSocket, so this only has to
			// be an address the client will dial — never one that answers. It is
			// set here rather than left to a gitignored .env.local so that CI and a
			// developer's machine run the same configuration; without it the
			// production build refuses to start, by design.
			PUBLIC_WS_URL: 'ws://127.0.0.1:5066/ws',
			// Exercises the deployed-dev-frontend configuration, which is the only
			// one where the ?ws= override is live.
			PUBLIC_ALLOW_WS_OVERRIDE: 'true',
			// Vite lets process.env win over .env* files, so a developer's
			// .env.local cannot turn the button off under the suite.
			PUBLIC_DEBUG: 'true'
		}
	},
	testMatch: '**/*.e2e.{ts,js}'
});
