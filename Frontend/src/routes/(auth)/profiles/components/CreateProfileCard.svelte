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

<div class="grid min-h-70 place-items-center rounded-lg border border-dashed border-line-strong">
	{#if open}
		<form
			class="grid w-full max-w-70 justify-items-center gap-(--sp-5) p-(--sp-8)"
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
	{:else}
		<div class="grid justify-items-center gap-(--sp-5)">
			<div class="grid justify-items-center gap-(--sp-4) p-(--sp-8) text-center">
				<span class="grid size-10 place-items-center rounded-md bg-action-quiet text-text-faint">
					<UserPlus size={20} />
				</span>
				<span class="oi-display-sm text-text-muted">New profile</span>
				<span class="oi-body-sm max-w-70 text-pretty text-text-faint">
					Start a fresh character.
				</span>
			</div>
			<Button variant="primary" onclick={() => (open = true)}>
				<Plus />
				New profile
			</Button>
		</div>
	{/if}
</div>
