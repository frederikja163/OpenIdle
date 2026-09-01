<script lang="ts">
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { loadProfiles, profilesState } from '$lib/state/profiles.svelte';
	import { ensureLoggedIn, userState } from '$lib/state/user.svelte';

	/*
	 * The two calls that have to happen before most requests are worth sending: the
	 * socket must be logged in, and a profile must be selected.
	 *
	 * They go through the app's store functions rather than the raw request form
	 * next door, because those functions are what write userState and profilesState
	 * — and those stores are where the form's value dropdowns get their options. A
	 * ListProfiles sent by hand answers the console but leaves the store empty.
	 */
</script>

<Column class="gap-(--sp-6)">
	<Row class="items-baseline gap-(--sp-5)">
		<span class="oi-label-md text-text-strong">Session</span>
		<span class="oi-body-sm text-text-muted">{userState.status}</span>
		{#if profilesState.selectedProfileId}
			<span class="oi-body-sm text-text-faint">profile {profilesState.selectedProfileId}</span>
		{/if}
	</Row>

	<Row class="flex-wrap items-center gap-(--sp-5)">
		<Button onclick={() => void ensureLoggedIn()} disabled={userState.status === 'loggingIn'}>
			Login as test user
		</Button>
		<Button onclick={() => void loadProfiles()} disabled={profilesState.status === 'loading'}>
			Load profiles
		</Button>
		<span class="oi-body-sm text-text-faint">
			{profilesState.profiles.length}
			{profilesState.profiles.length === 1 ? 'profile' : 'profiles'} in the dropdowns
		</span>
	</Row>

	{#if userState.error}
		<span role="alert" class="oi-body-sm text-text-danger">{userState.error}</span>
	{/if}
	{#if profilesState.error}
		<span role="alert" class="oi-body-sm text-text-danger">{profilesState.error}</span>
	{/if}
</Column>
