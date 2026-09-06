<script lang="ts">
	import type { IconComponent } from '$lib/components/icon';
	import Row from '$lib/components/layout/Row.svelte';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System stat pill (StatPill.jsx): an inset-well chip pairing
	 * an icon with a label and a tabular value. The tone lands on the root so the
	 * icon and value inherit it through currentColor; only the label stays faint.
	 */
	type StatPillTone = 'neutral' | 'accent' | 'xp';

	interface Props {
		icon: IconComponent;
		label?: string;
		value: string | number;
		tone?: StatPillTone;
		class?: string;
	}

	let { icon: PillIcon, label, value, tone = 'neutral', class: className }: Props = $props();

	const tones: Record<StatPillTone, string> = {
		neutral: 'text-text-body',
		accent: 'text-text-accent',
		xp: 'text-text-xp'
	};
</script>

<Row
	class={cn(
		'items-center gap-(--sp-3) rounded-sm bg-surface-inset px-2.5 py-1.25 shadow-(--inset-well)',
		tones[tone],
		className
	)}
>
	<PillIcon size={13} />
	{#if label}
		<span class="oi-label-sm text-text-faint">{label}</span>
	{/if}
	<span class="oi-num-md">{value}</span>
</Row>
