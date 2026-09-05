<script lang="ts">
	import IconButton from '$lib/components/core/IconButton.svelte';
	import Meter from '$lib/components/core/Meter.svelte';
	import EmptyState from '$lib/components/feedback/EmptyState.svelte';
	import FloatingReward from '$lib/components/feedback/FloatingReward.svelte';
	import ActionCard from '$lib/components/game/ActionCard.svelte';
	import SkillRailItem from '$lib/components/game/SkillRailItem.svelte';
	import Column from '$lib/components/layout/Column.svelte';
	import Panel from '$lib/components/layout/Panel.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import { itemMeta } from '$lib/game/catalog';
	import type {
		BoardReward,
		GameAction,
		PlayableSkillId,
		RunningAction,
		Skill
	} from '$lib/game/types';
	import type { ItemId } from '$lib/ws/protocol';
	import Lock from '@lucide/svelte/icons/lock';
	import PanelLeftClose from '@lucide/svelte/icons/panel-left-close';
	import PanelLeftOpen from '@lucide/svelte/icons/panel-left-open';
	import Square from '@lucide/svelte/icons/square';

	/*
	 * The skill rail and the action grid for whichever skill is selected. The rail
	 * collapses to glyphs only; that is local view state, not board state, so it
	 * lives here rather than on the page.
	 */
	interface Props {
		skills: Skill[];
		actions: Record<PlayableSkillId, GameAction[]>;
		activeSkill: PlayableSkillId;
		running: RunningAction | null;
		progress: number;
		reward: BoardReward | null;
		held: Partial<Record<ItemId, number>>;
		onSelectSkill: (id: PlayableSkillId) => void;
		onStartAction: (action: GameAction) => void;
		onStopAction: () => void;
	}

	let {
		skills,
		actions,
		activeSkill,
		running,
		progress,
		reward,
		held,
		onSelectSkill,
		onStartAction,
		onStopAction
	}: Props = $props();

	let collapsed = $state(false);

	const skill = $derived(skills.find((s) => s.id === activeSkill) ?? skills[0]);
	const list = $derived(actions[activeSkill] ?? []);
	const runningAction = $derived(
		running
			? Object.values(actions)
					.flat()
					.find((action) => action.id === running.action)
			: undefined
	);

	// An input's id is an ItemId by construction (the catalog reads it off the
	// contract); ActionInput types it as a string because the card is generic.
	function have(input: { id: string }): number {
		return held[input.id as ItemId] ?? 0;
	}

	function hasMaterials(action: GameAction): boolean {
		return (action.inputs ?? []).every((input) => have(input) >= input.qty);
	}

	function isLocked(action: GameAction) {
		return (action.lockedAt != null && action.lockedAt > skill.level) || !hasMaterials(action);
	}
</script>

<Panel title="Skills" padded={false} class="min-h-0 flex-1">
	{#snippet icon()}
		<IconButton
			icon={collapsed ? PanelLeftOpen : PanelLeftClose}
			label="Toggle rail"
			size="sm"
			onclick={() => (collapsed = !collapsed)}
		/>
	{/snippet}

	<!--
		The header stop names the action outright rather than pairing a status badge
		with a bare verb, so one control is the whole answer to "what is running".
		The name is capped and truncated because it is what varies: the header must
		not resize as the board moves from Talc Ore to Calcite Pickaxe Head.
	-->
	{#snippet actions()}
		{#if runningAction}
			<Button variant="danger" size="sm" title="Stop {runningAction.name}" onclick={onStopAction}>
				<Square fill="currentColor" />
				<span class="max-w-45 truncate">Stop {runningAction.name}</span>
			</Button>
		{/if}
	{/snippet}

	<div
		class="grid h-full min-h-0"
		style:grid-template-columns="{collapsed ? '58px' : 'var(--w-skillrail)'} 1fr"
	>
		<div
			class="oi-scroll grid content-start gap-0.75 overflow-y-auto border-r border-line-hairline px-(--sp-4) py-(--sp-5)"
		>
			{#each skills as railSkill (railSkill.id)}
				<SkillRailItem
					name={railSkill.name}
					icon={railSkill.icon}
					level={railSkill.level}
					xp={railSkill.xp}
					xpMax={railSkill.xpMax}
					{collapsed}
					active={railSkill.id === activeSkill}
					running={running?.skill === railSkill.id}
					onclick={() => onSelectSkill(railSkill.id)}
				/>
			{/each}
		</div>

		<div class="flex min-h-0 flex-col">
			<Row
				class="items-center gap-(--sp-6) border-b border-line-hairline px-(--gutter-panel) py-(--sp-5)"
			>
				<Row class="items-center gap-(--sp-4)">
					<span class="text-verdant-300"><skill.icon size={17} /></span>
					<span class="oi-display-md text-text-strong">{skill.name}</span>
					<Badge variant="xp">Lv {skill.level}</Badge>
				</Row>
				<span class="max-w-65 flex-1">
					<!--
						Keyed on the skill so switching rebuilds the meter instead of sliding
						it. The fill's 1000ms transition is there to interpolate one skill's
						XP between ticks; run across a change of subject it spends a second
						showing a level-0 skill part-full.
					-->
					{#key activeSkill}
						<Meter
							value={skill.xp}
							max={skill.xpMax}
							tone="xp"
							size="sm"
							label="{skill.name} experience"
						/>
					{/key}
				</span>
				<span class="oi-num-sm text-text-faint">{skill.xp}/{skill.xpMax} XP</span>
			</Row>

			<div class="oi-scroll min-h-0 flex-1 overflow-y-auto p-(--gutter-panel)">
				{#if list.length === 0}
					<EmptyState
						icon={Lock}
						title="{skill.name} has no actions"
						hint="Nothing to run here yet."
					/>
				{:else}
					<div class="grid grid-cols-[repeat(auto-fill,var(--size-action-card))] gap-(--gap-grid)">
						{#each list as action (action.id)}
							{@const isRunning = running?.action === action.id}
							{@const locked = isLocked(action)}
							{@const lockedBy = hasMaterials(action) ? 'level' : 'items'}
							<div class="relative">
								<ActionCard
									name={action.name}
									glyph={action.glyph}
									rarity={action.rarity}
									yieldQty={action.qty}
									duration="{Math.round(action.ms / 1000)}s"
									xp={action.xp}
									running={isRunning}
									progress={isRunning ? progress : 0}
									inputs={action.inputs?.map((input) => ({ ...input, have: have(input) }))}
									{locked}
									lockedAt={action.lockedAt}
									{lockedBy}
									lockedSkill={skill.name}
									skillLevel={skill.level}
									onclick={() => onStartAction(action)}
									onStop={onStopAction}
								/>
								{#if reward?.action === action.id}
									<!--
										FloatingReward is a one-shot animation with no exit state, so
										the same action paying out again has to remount it to replay.
										The key counter on the board changes every completion.

										Left corner and pointer-events-none, both because the running
										card's stop button owns the right one: an overlay that takes
										the pointer there steals the hover the button is revealed by,
										so it would vanish under the cursor on every payout.
									-->
									{#key reward.key}
										<!--
											What the payout actually changed rather than what the card
											promises: a bonus drop pays +3 on a card that says ×2, and
											the costs it consumed are the card's own inputs, not loot.
										-->
										<Column class="pointer-events-none absolute top-1 left-2.5 items-start gap-1">
											{#if reward.xp > 0}
												<FloatingReward amount="+{reward.xp} XP" tone="xp" />
											{/if}
											{#each reward.items.filter((line) => line.delta > 0) as line (line.itemId)}
												<FloatingReward
													icon={itemMeta(line.itemId).glyph}
													amount="+{line.delta}"
													tone="loot"
												/>
											{/each}
										</Column>
									{/key}
								{/if}
							</div>
						{/each}
					</div>
				{/if}
			</div>
		</div>
	</div>
</Panel>
