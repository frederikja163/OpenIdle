// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
	namespace App {
		// interface Error {}
		// interface Locals {}
		// interface PageData {}
		// interface PageState {}
		// interface Platform {}
	}

	/**
	 * The build this bundle came from, inlined by the `define` in vite.config.ts.
	 * Nulls mean a build outside CI, which the version footer shows as local.
	 */
	const __OPENIDLE_BUILD__: {
		readonly commit: string | null;
		readonly commitTime: number | null;
	};
}

export {};
