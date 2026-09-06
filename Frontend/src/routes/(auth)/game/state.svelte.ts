import { actionById, INVENTORY_ORDER, ITEMS, SKILL_ORDER, SKILLS } from '$lib/game/catalog';
import { levelProgress } from '$lib/game/level-curve';
import type { GameAction, InventoryItem, PlayableSkillId, Skill } from '$lib/game/types';
import { gameState } from '$lib/state/game.svelte';

/*
 * The board's view of the game store: the server's totals dressed for the
 * components, plus the two things only the board owns — which skill's actions
 * are showing, and a clock for the running action's meter. The store holds
 * when the cycle began; the meter is that against the clock, so nothing here
 * has to be told about a payout to snap back to zero.
 *
 * What the store already exposes in the shape the panels want — the pack, the
 * last payout, the actions themselves — is read from it directly by the page
 * rather than mirrored here.
 */

/** How often the meter advances between payouts. */
const TICK_MS = 100;

export class BoardState {
	/** The rail selection. Null follows whatever is running, so a payout that names the activity after a reload also picks its skill. */
	activeSkill = $state<PlayableSkillId | null>(null);
	#now = $state(Date.now());

	skills = $derived<Skill[]>(
		SKILL_ORDER.map((id) => {
			const dto = gameState.skills[id];
			const progress = levelProgress(dto?.xp ?? 0, dto?.level ?? 1);
			return { id, ...SKILLS[id], level: progress.level, xp: progress.into, xpMax: progress.span };
		})
	);

	inventory = $derived<InventoryItem[]>(
		INVENTORY_ORDER.filter((id) => (gameState.items[id] ?? 0) > 0).map((id) => ({
			id,
			...ITEMS[id],
			count: gameState.items[id] ?? 0
		}))
	);

	/** The running action itself, so nothing downstream has to look it up again. */
	running = $derived<GameAction | null>(
		gameState.running ? (actionById(gameState.running.activityId) ?? null) : null
	);

	// Clamped rather than wrapped: a late payout should show a full bar
	// waiting, not a second cycle the server never confirmed. The payout moves
	// the start, and the meter snaps to zero on that drop.
	progress = $derived.by(() => {
		const current = gameState.running;
		const action = this.running;
		if (!current || !action) {
			return 0;
		}
		return Math.min(100, ((this.#now - current.startedAt) / action.ms) * 100);
	});

	totalLevel = $derived(this.skills.reduce((total, skill) => total + skill.level, 0));

	selectedSkill = $derived<PlayableSkillId>(
		this.activeSkill ?? this.running?.skill ?? SKILL_ORDER[0]
	);

	/**
	 * Runs the clock until the returned teardown is called. The page drives this
	 * from an `$effect` keyed on the running activity, so a stop halts it.
	 */
	run(): () => void {
		if (!gameState.running) {
			return () => {};
		}
		this.#now = Date.now();
		const timer = setInterval(() => (this.#now = Date.now()), TICK_MS);
		return () => clearInterval(timer);
	}
}
