import { defineConfig } from '@playwright/test';

export default defineConfig({
	webServer: {
		command: 'bun run build && bun run preview',
		port: 4173,
		env: {
			// Vite lets process.env win over .env* files, so a developer's
			// .env.local cannot turn the button off under the suite.
			PUBLIC_DEBUG: 'true'
		}
	},
	testMatch: '**/*.e2e.{ts,js}'
});
