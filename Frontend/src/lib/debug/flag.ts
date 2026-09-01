import { env } from '$env/dynamic/public';

// Read lazily (never as a module-level const) so a server render can consult the
// runtime value: on the server $env/dynamic/public is populated by the process
// at request time, and a build step that loads the module early would settle it
// empty. Only the literal `true` counts, so a typo fails closed.
export function debugEnabled(): boolean {
	return env.PUBLIC_DEBUG === 'true';
}
