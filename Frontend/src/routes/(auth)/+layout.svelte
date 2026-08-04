<script lang="ts">
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import Gamepad2 from '@lucide/svelte/icons/gamepad-2';
	import InfinityIcon from '@lucide/svelte/icons/infinity';
	import Users from '@lucide/svelte/icons/users';
	import githubMark from '$lib/assets/github-mark-white.svg';
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
			class="group inline-flex size-6.5 items-center justify-center rounded-sm border border-transparent no-underline transition-[background-color] duration-(--dur-fast) ease-out hover:bg-action-quiet"
		>
			<!--
				GitHub's Invertocat, shipped verbatim from their own logo pack
				(https://github.com/logos) — their guidelines forbid recolouring it,
				so unlike every other icon in the chrome this one cannot tint from
				the token layer. It stays white and dims to sit at the muted weight
				of its neighbours instead. The anchor's aria-label already names the
				link, so the mark itself is decorative.
			-->
			<img
				src={githubMark}
				alt=""
				class="size-3.75 opacity-50 transition-opacity duration-(--dur-fast) ease-out group-hover:opacity-100"
			/>
		</a>
	</Row>
</header>

{@render children()}
