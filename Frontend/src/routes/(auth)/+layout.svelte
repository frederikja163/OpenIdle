<script lang="ts">
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import Gamepad2 from '@lucide/svelte/icons/gamepad-2';
	import InfinityIcon from '@lucide/svelte/icons/infinity';
	import Users from '@lucide/svelte/icons/users';
	import Row from '$lib/components/layout/Row.svelte';
	import { cn } from '$lib/utils/stylingUtils';

	let { children } = $props();

	// resolve() applies the configured base path and type-checks each pathname
	// against the real route tree, so a renamed route fails the build here
	// rather than shipping a dead link in the chrome.
	const views = [
		{ href: resolve('/game'), label: 'Game', icon: Gamepad2 },
		{ href: resolve('/profiles'), label: 'Profiles', icon: Users }
	];

	const repository = 'https://github.com/frederikja163/OpenIdle';

	/** A view stays lit for everything nested beneath it, not just its index. */
	function isActive(href: string) {
		return page.url.pathname === href || page.url.pathname.startsWith(`${href}/`);
	}
</script>

<!--
	Top chrome, per the OpenIdle design system's TopBar: a fixed 52px band that
	never reflows — wordmark left, view nav beside it, links right.

	`rounded-xs`/`rounded-sm` and `ease-out` are the design system's own values,
	not Tailwind's defaults: layout.css remaps the radius scale to the machined
	3/5/8/12/16 and the token layer overrides `--ease-out`.
-->
<header
	class="flex h-(--h-topbar) shrink-0 items-center gap-(--sp-6) border-b border-line-soft bg-surface-chrome px-(--gutter-app) shadow-(--shadow-card)"
>
	<Row class="items-center gap-(--sp-3)">
		<!--
			No logo mark exists for OpenIdle. The design system's sanctioned
			stand-in is a verdant tile beside the wordmark set in Chakra Petch —
			do not draw one. The wordmark is always one word, capital O and I.
		-->
		<span
			class="inline-flex size-5.5 items-center justify-center rounded-xs bg-verdant-600 text-action-primary-text"
		>
			<InfinityIcon size={14} />
		</span>
		<span class="oi-display-md text-text-strong">OpenIdle</span>
	</Row>

	<nav class="flex items-center gap-(--sp-1)">
		{#each views as view (view.href)}
			{@const active = isActive(view.href)}
			<a
				href={view.href}
				aria-current={active ? 'page' : undefined}
				class={cn(
					'oi-label-md inline-flex items-center gap-(--sp-3) rounded-sm border border-transparent px-2.75 py-1.5 no-underline transition-[background-color,color] duration-(--dur-fast) ease-out',
					active
						? 'border-line-accent bg-surface-active text-text-accent'
						: 'text-text-faint hover:text-text-body'
				)}
			>
				<view.icon size={13} />
				{view.label}
			</a>
		{/each}
	</nav>

	<Row class="ml-auto items-center gap-(--sp-2)">
		<a
			href={repository}
			target="_blank"
			rel="noreferrer"
			aria-label="Source"
			title="Source"
			class="inline-flex size-6.5 items-center justify-center rounded-sm border border-transparent text-text-muted no-underline transition-[background-color,color] duration-(--dur-fast) ease-out hover:bg-action-quiet hover:text-text-body"
		>
			<!--
				Lucide dropped its brand marks, so there is no `github` icon in
				@lucide/svelte to import. The mark is inlined instead, filled
				with currentColor so it still tints from the token layer like
				every other icon in the chrome.
			-->
			<svg width="15" height="15" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
				<path
					d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27s1.36.09 2 .27c1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8Z"
				/>
			</svg>
		</a>
	</Row>
</header>

{@render children()}
