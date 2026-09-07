import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';
import { playwright } from '@vitest/browser-playwright';
import adapter from '@sveltejs/adapter-node';
import { sveltekit } from '@sveltejs/kit/vite';

/*
 * Build provenance for the version footer. Read from the environment of the
 * `vite build` that produced this bundle — CI hands the published commit and its
 * committer date (unix seconds) to Frontend/Dockerfile as build-args — and
 * inlined as a literal, so the served JavaScript names the commit it was built
 * from. Deliberately not a PUBLIC_* variable: those are read at runtime from the
 * container's environment and describe where the image is deployed, whereas
 * this describes the image itself and must not change after the build. Absent
 * values, as in `bun run dev`, become nulls the footer shows as a local build.
 */
const commitTime = process.env.GIT_COMMIT_TIME;
const build = {
	commit: process.env.GIT_COMMIT || null,
	commitTime: commitTime && /^\d+$/.test(commitTime) ? Number(commitTime) * 1000 : null
};

export default defineConfig({
	define: {
		__OPENIDLE_BUILD__: JSON.stringify(build)
	},
	plugins: [
		tailwindcss(),
		sveltekit({
			compilerOptions: {
				// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
				runes: ({ filename }) =>
					filename.split(/[/\\]/).includes('node_modules') ? undefined : true
			},

			// The deployment target is a self-hosted host running the image built by
			// Frontend/Dockerfile, so the adapter is pinned rather than detected. adapter-node
			// keeps a SvelteKit server process alive, which is what makes `$env/dynamic/public`
			// (PUBLIC_WS_URL) a genuine runtime variable and lets one image serve every
			// environment. See doc/libraries/sveltejs-adapter-node.md.
			adapter: adapter()
		})
	],
	test: {
		expect: { requireAssertions: true },
		projects: [
			{
				extends: './vite.config.ts',
				test: {
					name: 'server',
					environment: 'node',
					include: ['src/**/*.{test,spec}.{js,ts}']
				}
			},
			{
				extends: './vite.config.ts',
				test: {
					name: 'client',
					// Svelte component tests run in a real browser via the
					// already-installed Playwright (chromium). This is what the
					// `server` project's old `*.svelte.{test,spec}.*` exclusion
					// anticipated; without it those tests were collected by no
					// project and passed green without executing.
					include: ['src/**/*.svelte.{test,spec}.{js,ts}'],
					browser: {
						enabled: true,
						provider: playwright(),
						instances: [{ browser: 'chromium' }]
					}
				}
			}
		]
	}
});
