<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { Button } from '$lib/components/ui/button';
	import { ensureLoggedIn, userState, type LoginStatus } from '$lib/state/user.svelte';

	const statusLabel: Record<LoginStatus, string> = {
		loggedOut: 'Signed out',
		loggingIn: 'Signing in…',
		loggedIn: 'Signed in',
		error: 'Sign-in failed'
	};

	/**
	 * redirectTo comes from the (auth) guard, which records the internal
	 * pathname the visitor was turned away from. Reject anything that could
	 * point a post-login navigation off-site — absolute and protocol-relative
	 * URLs are never valid internal redirect targets.
	 */
	function isSafeRedirect(path: string): boolean {
		return path.startsWith('/') && !path.startsWith('//') && !/^[a-z][a-z0-9+.-]*:/i.test(path);
	}

	// /login is only for logged-out visitors: fires both when the login below
	// succeeds and when an already-logged-in user navigates here. replaceState
	// for the same reason the (auth) guard uses it — a login screen the visitor
	// has since passed is not somewhere Back should return to.
	$effect(() => {
		if (userState.status === 'loggedIn') {
			// redirectTo is a validated internal path rather than a route literal,
			// so it cannot be expressed through resolve(); the /profiles fallback
			// still goes through it.
			const redirectTo = page.url.searchParams.get('redirectTo');
			const target = redirectTo && isSafeRedirect(redirectTo) ? redirectTo : resolve('/profiles');
			// eslint-disable-next-line svelte/no-navigation-without-resolve
			void goto(target, { replaceState: true });
		}
	});
</script>

<h1>Login</h1>
<p data-testid="login-status">{statusLabel[userState.status]}</p>
<!--
	Only 'loggingIn' disables the button: after an error it has to stay live,
	since retrying is what clears the error.
-->
<Button
	variant="primary"
	size="md"
	disabled={userState.status === 'loggingIn'}
	onclick={() => void ensureLoggedIn()}
>
	Log in
</Button>
{#if userState.error}
	<p>{userState.error}</p>
{/if}
