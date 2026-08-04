<script lang="ts">
	import Plus from '@lucide/svelte/icons/plus';
	import UserPlus from '@lucide/svelte/icons/user-plus';
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { profiles, slotCapacity } from './data';
	import ProfileCard from './components/ProfileCard.svelte';
</script>

<!--
	Profiles: a grid of save-slot panels plus one dashed create card, per the
	design system's profiles template. The page is static mock data — the empty
	slot is inlined here because it is a single, prop-less use; promote it to a
	shared EmptyState only when a second consumer appears.
-->
<Column class="mx-auto w-full max-w-[1600px] gap-(--gutter-app) p-(--gutter-app)">
	<Row class="items-baseline gap-(--sp-5)">
		<h1 class="oi-display-lg text-text-strong">Profiles</h1>
		<span class="oi-body-md text-text-muted">{profiles.length} of {slotCapacity} slots used</span>
	</Row>
	<div
		class="grid grid-cols-[repeat(auto-fill,minmax(360px,1fr))] content-start gap-(--gutter-app)"
	>
		{#each profiles as profile (profile.name)}
			<ProfileCard {profile} />
		{/each}
		<!-- empty save slot -->
		<div
			class="grid min-h-70 place-items-center rounded-lg border border-dashed border-line-strong"
		>
			<div class="grid justify-items-center gap-(--sp-5)">
				<div class="grid justify-items-center gap-(--sp-4) p-(--sp-8) text-center">
					<span class="grid size-10 place-items-center rounded-md bg-action-quiet text-text-faint">
						<UserPlus size={20} />
					</span>
					<span class="oi-display-sm text-text-muted">Empty slot</span>
					<span class="oi-body-sm max-w-70 text-pretty text-text-faint">
						Start a fresh character on this save slot.
					</span>
				</div>
				<Button variant="primary">
					<Plus />
					New profile
				</Button>
			</div>
		</div>
	</div>
</Column>
