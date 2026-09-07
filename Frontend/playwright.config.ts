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
			// Deliberately not the socket's port: the version footer fetches from
			// here, and a different port is what lets a test tell "the configured
			// API" apart from "the socket's host" — nothing listens on either.
			PUBLIC_API_URL: 'http://127.0.0.1:5067',
			// Exercises the deployed-dev-frontend configuration, which is the only
			// one where the ?ws= override is live.
			PUBLIC_ALLOW_WS_OVERRIDE: 'true',
			// Vite lets process.env win over .env* files, so a developer's
			// .env.local cannot turn the button off under the suite.
			PUBLIC_DEBUG: 'true',
			// A known build for the version footer to show: the suite builds
			// outside CI and would otherwise read `local`. The time is
			// 2026-09-04 23:26:40 UTC.
			GIT_COMMIT: '1e1c256a0b1c2d3e4f5061728394a5b6c7d8e9f0',
			GIT_COMMIT_TIME: '1788564400'
		}
	},
	testMatch: '**/*.e2e.{ts,js}'
});
