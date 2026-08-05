<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { Button } from '$lib/components/ui/button';
	import { ensureLoggedIn, userState } from '$lib/state/user.svelte';

	// /login is only for logged-out visitors: fires both when the login below
	// succeeds and when an already-logged-in user navigates here. replaceState
	// for the same reason the (auth) guard uses it — a login screen the visitor
	// has since passed is not somewhere Back should return to.
	$effect(() => {
		if (userState.status === 'loggedIn') {
			void goto(resolve('/profiles'), { replaceState: true });
		}
	});
</script>

<h1>Login</h1>
<p>{userState.status}</p>
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
