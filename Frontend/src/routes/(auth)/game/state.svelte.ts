import * as mock from './data';
import type { GameAction } from './data';

/*
 * The board's idle loop, simulated in the browser. Ported from the design
 * system's GameBoard.jsx, which owns exactly this state and this interval.
 *
 * Nothing here talks to the backend yet — there are no game messages in the
 * protocol. Keeping the whole simulation in one class is what makes that
 * swappable later: the components and the page markup only read the fields
 * below, so wiring the socket up means reimplementing this file, not the UI.
 */
export interface RunningAction {
	skill: string;
	action: string;
	ms: number;
	xp: number;
}

/** How often the loop advances. The action meter interpolates between ticks. */
const TICK_MS = 100;

export class BoardState {
	skills = $state(mock.skills.map((skill) => ({ ...skill })));
	inventory = $state(mock.inventory.map((item) => ({ ...item })));
	activeSkill = $state('mining');
	running = $state<RunningAction | null>({
		skill: 'mining',
		action: 'talc',
		ms: 5000,
		xp: 1
	});
	progress = $state(0);
	reward = $state<{ action: string; key: number } | null>(null);
	rate = $state(12.4);

	/** Increments on every payout purely to remount the floating reward. */
	#rewardKey = 0;

	readonly actions: Record<string, GameAction[]> = mock.actions;
	readonly slotCapacity = mock.slotCapacity;

	totalLevel = $derived(this.skills.reduce((total, skill) => total + skill.level, 0));

	/** Item id → count, so an action card can tell whether its inputs are covered. */
	held = $derived(
		Object.fromEntries(this.inventory.map((item) => [item.id, item.count])) as Record<
			string,
			number
		>
	);

	/**
	 * Runs the loop until the returned teardown is called. The page drives this
	 * from an `$effect` keyed on `running`, so switching actions restarts it.
	 */
	run(): () => void {
		const active = this.running;
		if (!active) return () => {};

		const step = 100 / (active.ms / TICK_MS);
		const timer = setInterval(() => {
			if (this.progress >= 100) {
				this.#complete(active);
				this.progress = 0;
				return;
			}
			// The last tick clamps at exactly a full bar, so the meter spends a
			// drawn 100% before the completion payout snaps it straight back to 0.
			this.progress = Math.min(100, this.progress + step);
		}, TICK_MS);

		return () => clearInterval(timer);
	}

	/** Starts an action. Pressing an already-running action is a no-op. */
	start(action: GameAction): void {
		if (this.running?.action === action.id) return;
		// An action whose inputs are not covered cannot start. The card already
		// refuses the click, but the model stays authoritative on its own.
		const short = (action.inputs ?? []).some((input) => (this.held[input.id] ?? 0) < input.qty);
		if (short) return;
		this.progress = 0;
		this.running = {
			skill: this.activeSkill,
			action: action.id,
			ms: action.ms,
			xp: action.xp
		};
	}

	/** Stops the running action, if any, and restarts its meter at zero. */
	stop(): void {
		this.running = null;
		this.progress = 0;
	}

	#complete(active: RunningAction): void {
		this.#awardXp(active.skill, active.xp);
		this.#collect(active.action);
		this.reward = { action: active.action, key: ++this.#rewardKey };
		this.rate = Math.round((this.rate + 0.1) * 10) / 10;
	}

	#awardXp(skillId: string, amount: number): void {
		const skill = this.skills.find((candidate) => candidate.id === skillId);
		if (!skill) return;

		const xp = skill.xp + amount;
		// The overflow carries into the next level rather than being dropped, so a
		// long action never wastes the part of its payout that crossed the line.
		if (xp >= skill.xpMax) {
			skill.level += 1;
			skill.xp = xp - skill.xpMax;
		} else {
			skill.xp = xp;
		}
	}

	#collect(itemId: string): void {
		const item = this.inventory.find((candidate) => candidate.id === itemId);
		if (item) item.count += 1;
	}
}
