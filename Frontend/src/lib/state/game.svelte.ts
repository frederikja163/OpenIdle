import { canAfford } from '$lib/game/catalog';
import type { BoardReward } from '$lib/game/types';
import { getWsClient, type WsClient } from '$lib/ws/client';
import type { ActivityId, ItemId, ServerEventOf, SkillDto, SkillId } from '$lib/ws/protocol';
import { sessionRun, sessionState } from './session.svelte';

export type GameStatus = 'idle' | 'loading' | 'loaded' | 'error';

export interface RunningActivity {
	activityId: ActivityId;
	/** When the current cycle began, on the client clock. */
	startedAt: number;
}

/*
 * The selected profile's game, as the socket reports it. Skills and items are
 * the server's absolute totals keyed by id; the rest is what only this side
 * knows — the activity it believes is running and the last payout it saw.
 */
export const gameState = sessionState(() => ({
	status: 'idle' as GameStatus,
	error: null as string | null,
	skills: {} as Partial<Record<SkillId, SkillDto>>,
	items: {} as Partial<Record<ItemId, number>>,
	// Nothing on the wire reports it, so after a reload this stays null until
	// the next payout names it — see applyActivityEnded.
	running: null as RunningActivity | null,
	// One start or stop at a time: the backend handles frames FIFO, so two in
	// flight would both apply and leave `running` disagreeing with the socket.
	pending: null as ActivityId | 'stop' | null,
	actionError: null as string | null,
	lastReward: null as BoardReward | null
}));

const ALREADY_DOING = 'Profile is already doing an activity.';
const NOT_DOING = 'Profile is not doing an activity.';

/** Increments on every payout purely to remount the floating reward. */
let rewardKey = 0;

// A payout can commit between the load's database read and its response, in
// which case the event is the newer of the two for the rows it touched. The
// keys are collected while a load is in flight and win over the response.
// Plain arrays rather than sets: this is bookkeeping nothing renders, and a
// key listed twice is overlaid twice to the same effect.
let loadOverlay: { skills: SkillId[]; items: ItemId[] } | null = null;

export async function loadGame(): Promise<void> {
	if (gameState.status === 'loading') {
		return;
	}
	gameState.status = 'loading';
	gameState.error = null;
	const overlay = { skills: [] as SkillId[], items: [] as ItemId[] };
	loadOverlay = overlay;
	await sessionRun(
		async () => {
			const client = getWsClient();
			const [skills, items] = await Promise.all([
				client.request('GetSkillsRequest', {}),
				client.request('GetItemsRequest', {})
			]);
			return { skills: skills.skills, items: items.items };
		},
		{
			ok: ({ skills, items }) => {
				const nextSkills: Partial<Record<SkillId, SkillDto>> = {};
				for (const skill of skills) {
					nextSkills[skill.skillId] = skill;
				}
				const nextItems: Partial<Record<ItemId, number>> = {};
				for (const item of items) {
					nextItems[item.itemId] = item.count;
				}
				for (const id of overlay.skills) {
					nextSkills[id] = gameState.skills[id];
				}
				for (const id of overlay.items) {
					nextItems[id] = gameState.items[id];
				}
				gameState.skills = nextSkills;
				gameState.items = nextItems;
				gameState.status = 'loaded';
			},
			fail: (message) => {
				gameState.status = 'error';
				gameState.error = message;
			}
		}
	);
	if (loadOverlay === overlay) {
		loadOverlay = null;
	}
}

/**
 * Applies a payout. Despite its name the event is not a stop: the backend
 * sends one per completed cycle and the activity keeps going, so it is also
 * how the store learns what is running after a reload.
 */
export function applyActivityEnded(event: ServerEventOf<'ActivityEndedEvent'>): void {
	const loaded = gameState.status === 'loaded';
	let xp = 0;
	for (const skill of event.skills) {
		xp += skill.xp - (gameState.skills[skill.skillId]?.xp ?? 0);
		gameState.skills[skill.skillId] = skill;
		loadOverlay?.skills.push(skill.skillId);
	}
	const items: BoardReward['items'] = [];
	for (const item of event.items) {
		const delta = item.count - (gameState.items[item.itemId] ?? 0);
		if (delta !== 0) {
			items.push({ itemId: item.itemId, delta });
		}
		gameState.items[item.itemId] = item.count;
		loadOverlay?.items.push(item.itemId);
	}
	gameState.running = { activityId: event.activityId, startedAt: Date.now() };
	// Before the load there is no baseline: the deltas above would be the
	// profile's whole history, and a catch-up batch would float "+31219 XP".
	if (loaded && (xp !== 0 || items.length > 0)) {
		gameState.lastReward = { action: event.activityId, key: ++rewardKey, xp, items };
	}
	// The backend checks the costs again at the next deadline and stops without
	// a word if they are short, so this is that stop, predicted from the same
	// numbers. Nothing more would ever be paid out anyway.
	if (!canAfford(event.activityId, gameState.items)) {
		gameState.running = null;
	}
}

function isMessage(error: unknown, message: string): boolean {
	return error instanceof Error && error.message === message;
}

/** Stops whatever the socket is doing; nothing running counts as stopped. */
async function stopQuietly(client: WsClient): Promise<void> {
	try {
		await client.request('StopActivityRequest', {});
	} catch (error) {
		if (!isMessage(error, NOT_DOING)) {
			throw error;
		}
	}
}

/**
 * Starts an activity, stopping the running one first because the backend
 * refuses a start on top of another. Pressing the running action is a no-op.
 */
export async function startActivity(activityId: ActivityId): Promise<boolean> {
	if (gameState.pending !== null || gameState.running?.activityId === activityId) {
		return false;
	}
	gameState.pending = activityId;
	gameState.actionError = null;
	const outcome = await sessionRun(
		async () => {
			const client = getWsClient();
			if (gameState.running !== null) {
				await stopQuietly(client);
			}
			try {
				await client.request('StartActivityRequest', { activityId });
			} catch (error) {
				if (!isMessage(error, ALREADY_DOING)) {
					throw error;
				}
				// The socket is doing something this store never heard of — the page
				// was reloaded mid-activity, or another tab started one. Take over.
				await stopQuietly(client);
				await client.request('StartActivityRequest', { activityId });
			}
		},
		{
			ok: () => {
				gameState.running = { activityId, startedAt: Date.now() };
			},
			fail: (message) => {
				gameState.actionError = message;
			}
		}
	);
	// On 'stale' the reset already cleared this; assigning again is harmless.
	gameState.pending = null;
	return outcome === 'ok';
}

export async function stopActivity(): Promise<boolean> {
	if (gameState.pending !== null) {
		return false;
	}
	gameState.pending = 'stop';
	gameState.actionError = null;
	const outcome = await sessionRun(() => stopQuietly(getWsClient()), {
		ok: () => {
			gameState.running = null;
		},
		fail: (message) => {
			gameState.actionError = message;
		}
	});
	gameState.pending = null;
	return outcome === 'ok';
}
