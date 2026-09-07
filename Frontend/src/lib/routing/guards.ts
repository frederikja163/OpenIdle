import { resolve } from '$app/paths';

/**
 * Whether a path leads somewhere that plays a profile. The board is worthless
 * without one — it renders an empty state and a button back to /profiles — so
 * both the post-login redirect and the top bar ask this before offering it.
 *
 * The prefix arm covers routes nested under the board rather than the board
 * itself, and the exact arm is what keeps a sibling like /gamepad out.
 */
export function requiresProfile(path: string): boolean {
	const game = resolve('/game');
	return path === game || path.startsWith(`${game}/`);
}
