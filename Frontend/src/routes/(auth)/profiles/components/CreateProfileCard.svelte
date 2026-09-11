<script lang="ts">
	import Plus from '@lucide/svelte/icons/plus';
	import UserPlus from '@lucide/svelte/icons/user-plus';
	import Row from '$lib/components/layout/Row.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import {
		createProfile,
		MAX_PROFILE_NAME_LENGTH,
		profilesState
	} from '$lib/state/profiles.svelte';

	/*
	 * The dashed tile that closes the profiles grid, in its two states: a prompt,
	 * and the name form it opens into. It reads the create action straight off the
	 * state module rather than taking props, the same way the app chrome calls
	 * logout — there is one create form and one backend to point it at.
	 */
	let open = $state(false);
	let name = $state('');
	let field = $state<HTMLInputElement | null>(null);

	// The field does not exist until the form opens, so focus has to wait for the
	// DOM; `autofocus` is what svelte's a11y_autofocus rule forbids.
	$effect(() => {
		if (open) {
			field?.focus();
		}
	});

	async function submit(): Promise<void> {
		if (await createProfile(name)) {
			close();
		}
	}

	function close(): void {
		open = false;
		name = '';
		profilesState.createError = null;
	}
</script>

<!--
	The padding belongs here rather than on either state's own contents: the
	button below is a sibling of the prompt block, so padding placed inside that
	block leaves the button sitting on the card's border.

	No min-height either. The grid stretches this tile to the row, so a floor here
	would only ever push it past the profile cards it sits beside, and
	place-items-center centres whichever state is showing in the space that leaves.

	Both states sit in the same grid cell rather than swapping, so the card always
	reserves the height of the taller one. This card is the tallest thing in the
	grid and therefore sets the row height, so a swap between two states of
	different heights would resize every profile card on a button press.
	`invisible` is load-bearing over `hidden`: visibility:hidden keeps the layout
	box that holds the height open, while still dropping the element out of the
	accessibility tree and the tab order.
-->
<div class="grid place-items-center rounded-lg border border-dashed border-line-strong p-(--sp-8)">
	<form
		class="col-start-1 row-start-1 grid w-full max-w-70 justify-items-center gap-(--sp-5)"
		class:invisible={!open}
		onsubmit={(event) => {
			event.preventDefault();
			void submit();
		}}
	>
		<label class="oi-label-sm justify-self-start text-text-muted" for="new-profile-name">
			Profile name
		</label>
		<Input
			id="new-profile-name"
			bind:ref={field}
			bind:value={name}
			disabled={profilesState.creating}
			maxlength={MAX_PROFILE_NAME_LENGTH}
			autocomplete="off"
			spellcheck="false"
			aria-invalid={profilesState.createError !== null}
			placeholder="Thorin"
			onkeydown={(event) => {
				if (event.key === 'Escape') {
					close();
				}
			}}
		/>
		{#if profilesState.createError}
			<p role="alert" class="oi-body-sm justify-self-start text-pretty text-text-danger">
				{profilesState.createError}
			</p>
		{:else}
			<p class="oi-body-sm justify-self-start text-pretty text-text-faint">
				Letters and digits, up to {MAX_PROFILE_NAME_LENGTH} characters.
			</p>
		{/if}
		<Row class="gap-(--gap-stack)">
			<!-- Button defaults to type="button", so submitting needs this spelled out. -->
			<Button type="submit" variant="primary" disabled={profilesState.creating}>
				<Plus />
				{profilesState.creating ? 'Creating…' : 'Create'}
			</Button>
			<Button variant="ghost" disabled={profilesState.creating} onclick={close}>Cancel</Button>
		</Row>
	</form>
	<div
		class="col-start-1 row-start-1 grid justify-items-center gap-(--sp-6)"
		class:invisible={open}
	>
		<div class="grid justify-items-center gap-(--sp-4) text-center">
			<span class="grid size-10 place-items-center rounded-md bg-action-quiet text-text-faint">
				<UserPlus size={20} />
			</span>
			<span class="oi-display-sm text-text-muted">New profile</span>
			<span class="oi-body-sm max-w-70 text-pretty text-text-faint">Start a fresh character.</span>
		</div>
		<Button variant="primary" onclick={() => (open = true)}>
			<Plus />
			New profile
		</Button>
	</div>
</div>
