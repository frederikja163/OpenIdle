<script lang="ts">
	import type { Snippet } from 'svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import * as Card from '$lib/components/ui/card';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System panel (Panel.jsx) in its header-band form: a titled
	 * bar over a body that fills the remaining height and scrolls internally.
	 *
	 * Card.Root is already the port of the same Panel.jsx and carries all of its
	 * chrome, so this builds on it rather than restating surface, sheen, border,
	 * radius and shadow. `gap-0 py-0` drops the profile-card vertical rhythm,
	 * which the board's panels replace with the header hairline.
	 */
	interface Props {
		title?: string;
		icon?: Snippet;
		actions?: Snippet;
		padded?: boolean;
		class?: string;
		children?: Snippet;
	}

	let { title, icon, actions, padded = true, class: className, children }: Props = $props();
</script>

<Card.Root class={cn('gap-0 py-0', className)}>
	{#if title || actions}
		<!--
			Not a <header>: Card.Root is a plain div, so a <header> inside it is not
			scoped to a sectioning element and every panel would announce itself as a
			second banner landmark alongside the app chrome.
		-->
		<Row
			class="shrink-0 items-center gap-(--sp-5) border-b border-line-hairline px-(--card-spacing) py-2.5"
		>
			{@render icon?.()}
			{#if title}
				<!-- The panel title is a label treatment applied to display type:
				     uppercase and the wider label tracking on top of Card.Title's size. -->
				<h2 class="oi-display-sm tracking-(--track-label) text-text-strong uppercase">{title}</h2>
			{/if}
			<Row class="ml-auto items-center gap-(--sp-2)">
				{@render actions?.()}
			</Row>
		</Row>
	{/if}

	<div class={cn('oi-scroll min-h-0 flex-1', padded && 'p-(--card-spacing)')}>
		{@render children?.()}
	</div>
</Card.Root>
