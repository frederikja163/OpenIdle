<script lang="ts">
	import ChevronsUp from '@lucide/svelte/icons/chevrons-up';
	import type { IconComponent } from '$lib/components/icon';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * OpenIdle Design System floating reward (FloatingReward.jsx): the amount that
	 * drifts up and fades when an action pays out. It is a one-shot animation with
	 * no exit state, so the caller replays it by remounting — on the board that is
	 * a `{#key}` block around the reward.
	 */
	type RewardTone = 'xp' | 'loot' | 'neutral';

	interface Props {
		amount: string;
		icon?: IconComponent;
		tone?: RewardTone;
		class?: string;
	}

	let { amount, icon: RewardIcon = ChevronsUp, tone = 'xp', class: className }: Props = $props();

	const tones: Record<RewardTone, string> = {
		xp: 'text-text-xp',
		loot: 'text-text-accent',
		neutral: 'text-text-body'
	};
</script>

<span
	class={cn(
		'oi-num-md pointer-events-none inline-flex animate-[oi-float-up_3s_var(--ease-out)_forwards] items-center gap-1 text-shadow-[0_1px_6px_rgba(0,0,0,.9)]',
		tones[tone],
		className
	)}
>
	<RewardIcon size={12} />{amount}
</span>
