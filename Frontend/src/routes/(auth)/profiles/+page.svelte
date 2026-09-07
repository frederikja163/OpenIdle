<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import VersionFooter from '$lib/components/core/VersionFooter.svelte';
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { loadProfiles, profilesState, selectProfile } from '$lib/state/profiles.svelte';
	import { toProfile } from './data';
	import CreateProfileCard from './components/CreateProfileCard.svelte';
	import ProfileCard from './components/ProfileCard.svelte';

	// No login handshake here: (auth)/+layout.svelte renders this page only once
	// userState is 'loggedIn', so the socket is already authenticated — which
	// ListProfiles requires. Effects never run during SSR, so no browser guard.
	$effect(() => {
		if (profilesState.status === 'idle') {
			void loadProfiles();
		}
	});

	const cards = $derived(
		profilesState.profiles.map((dto, index) =>
			toProfile(dto, index, dto.profileId === profilesState.selectedProfileId)
		)
	);

	// The page owns the navigation rather than the card, matching how the login
	// page and the auth layout own theirs.
	async function loadIntoGame(profileId: string): Promise<void> {
		if (await selectProfile(profileId)) {
			await goto(resolve('/game'));
		}
	}
</script>

<!--
	Profiles: a grid of save-slot panels plus one dashed create card, per the
	design system's profiles template.
-->
<Column class="mx-auto w-full max-w-[1600px] grow gap-(--gutter-app) p-(--gutter-app)">
	<Row class="items-baseline gap-(--sp-5)">
		<h1 class="oi-display-lg text-text-strong">Profiles</h1>
		<span class="oi-body-md text-text-muted">
			{cards.length}
			{cards.length === 1 ? 'profile' : 'profiles'}
		</span>
		{#if profilesState.status === 'loading'}
			<span class="oi-body-md text-text-faint">Loading profiles…</span>
		{/if}
	</Row>
	{#if profilesState.error}
		<p role="alert" class="oi-body-md text-text-muted">{profilesState.error}</p>
	{/if}
	<!--
		A failed select is a session-level condition ("Profile does not belong to
		user."), not a property of one card, so it reads here rather than in a
		footer that is already three buttons wide.
	-->
	{#if profilesState.selectError}
		<p role="alert" class="oi-body-md text-text-danger">{profilesState.selectError}</p>
	{/if}
	<div
		class="grid grid-cols-[repeat(auto-fill,minmax(360px,1fr))] content-start gap-(--gutter-app)"
	>
		{#each cards as profile (profile.profileId)}
			<ProfileCard
				{profile}
				onSelect={() => void loadIntoGame(profile.profileId)}
				selecting={profilesState.selectingProfileId === profile.profileId}
				disabled={profilesState.selectingProfileId !== null}
			/>
		{/each}
		<CreateProfileCard />
	</div>
	<VersionFooter class="mt-auto" />
</Column>
