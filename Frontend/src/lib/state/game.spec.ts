import { beforeEach, describe, expect, it, vi } from 'vitest';
import type {
	ActivityId,
	ItemDto,
	ItemId,
	ServerEventOf,
	SkillDto,
	SkillId
} from '$lib/ws/protocol';

vi.mock('$lib/ws/client', async () => (await import('$lib/state/test-support')).clientModule);

const { connection, request, resetConnection } = await import('$lib/state/test-support');
const { applyActivityEnded, gameState, loadGame, startActivity, stopActivity } =
	await import('$lib/state/game.svelte');
const { forgetSessionIntent, resetSessionState } = await import('$lib/state/session.svelte');

const PROFILE = '11111111-1111-1111-1111-111111111111';
const ALREADY_DOING = 'Profile is already doing an activity.';
const NOT_DOING = 'Profile is not doing an activity.';

function skill(skillId: SkillId, xp: number, level: number): SkillDto {
	return { profileId: PROFILE, skillId, xp, level };
}

function item(itemId: ItemId, count: number): ItemDto {
	return { profileId: PROFILE, itemId, count };
}

interface World {
	skills?: SkillDto[];
	items?: ItemDto[];
}

function ended(activityId: ActivityId, payout: World = {}): ServerEventOf<'ActivityEndedEvent'> {
	return {
		$type: 'ActivityEndedEvent',
		eventId: 0,
		timestamp: 0,
		activityId,
		skills: payout.skills ?? [],
		items: payout.items ?? []
	};
}

function responseTo(type: string, world: World): unknown {
	if (type === 'GetSkillsRequest') {
		return { $type: 'GetSkillsResponse', requestId: 1, skills: world.skills ?? [] };
	}
	if (type === 'GetItemsRequest') {
		return { $type: 'GetItemsResponse', requestId: 2, items: world.items ?? [] };
	}
	return { $type: `${type.replace('Request', '')}Response`, requestId: 3 };
}

/** The whole backend, for cases that only need it to say yes. */
function respond(world: World = {}): void {
	request.mockImplementation((type: string) => Promise.resolve(responseTo(type, world)));
}

/** A backend that refuses some requests, in the order they arrive per type. */
function respondOrRefuse(refusals: Record<string, (string | null)[]>, world: World = {}): void {
	request.mockImplementation((type: string) => {
		const message = refusals[type]?.shift();
		return message ? Promise.reject(new Error(message)) : Promise.resolve(responseTo(type, world));
	});
}

async function loadWith(world: World): Promise<void> {
	respond(world);
	await loadGame();
}

beforeEach(() => {
	resetConnection();
	resetSessionState();
	forgetSessionIntent();
});

describe('loadGame', () => {
	it('asks for every skill and item and keys them by id', async () => {
		await loadWith({ skills: [skill('Mining', 1000, 2)], items: [item('Tin', 3)] });

		expect(request).toHaveBeenCalledWith('GetSkillsRequest', {});
		expect(request).toHaveBeenCalledWith('GetItemsRequest', {});
		expect(gameState.skills).toEqual({ Mining: skill('Mining', 1000, 2) });
		expect(gameState.items).toEqual({ Tin: 3 });
		expect(gameState.status).toBe('loaded');
		expect(gameState.error).toBeNull();
	});

	it('surfaces the backend message when the load fails', async () => {
		request.mockRejectedValue(new Error('You must select a profile first.'));

		await loadGame();

		expect(gameState.status).toBe('error');
		expect(gameState.error).toBe('You must select a profile first.');
		expect(gameState.skills).toEqual({});
	});

	it('ignores a second load while one is in flight', async () => {
		const releases: (() => void)[] = [];
		request.mockImplementation(
			(type: string) => new Promise((resolve) => releases.push(() => resolve(responseTo(type, {}))))
		);

		const first = loadGame();
		await loadGame();

		expect(request).toHaveBeenCalledTimes(2);
		for (const release of releases) {
			release();
		}
		await first;
		expect(gameState.status).toBe('loaded');
	});

	it('keeps the activity a catch-up payout reported before the load', async () => {
		applyActivityEnded(ended('MineTin'));

		await loadWith({});

		expect(gameState.running?.activityId).toBe('MineTin');
	});
});

describe('applyActivityEnded', () => {
	it('upserts the absolute totals and floats what changed', async () => {
		await loadWith({ skills: [skill('Mining', 100, 1)], items: [item('Tin', 3)] });

		applyActivityEnded(
			ended('MineTin', { skills: [skill('Mining', 300, 1)], items: [item('Tin', 5)] })
		);

		expect(gameState.skills.Mining?.xp).toBe(300);
		expect(gameState.items.Tin).toBe(5);
		expect(gameState.lastReward).toMatchObject({
			action: 'MineTin',
			xp: 200,
			items: [{ itemId: 'Tin', delta: 2 }]
		});
		expect(gameState.running?.activityId).toBe('MineTin');
	});

	it('gives every payout a new key so the float remounts', async () => {
		await loadWith({});

		applyActivityEnded(ended('MineTin', { items: [item('Tin', 2)] }));
		const first = gameState.lastReward?.key;
		applyActivityEnded(ended('MineTin', { items: [item('Tin', 4)] }));

		expect(gameState.lastReward?.key).not.toBe(first);
	});

	it('floats nothing when the payout changed nothing', async () => {
		await loadWith({});

		applyActivityEnded(ended('MineTin'));

		expect(gameState.lastReward).toBeNull();
		expect(gameState.running?.activityId).toBe('MineTin');
	});

	it('floats nothing before the board is loaded', () => {
		applyActivityEnded(ended('MineTin', { items: [item('Tin', 40)] }));

		expect(gameState.lastReward).toBeNull();
		expect(gameState.items.Tin).toBe(40);
		expect(gameState.running?.activityId).toBe('MineTin');
	});

	it('stops the activity itself once the payout leaves its costs short', async () => {
		// Crafting a handle costs one balsa, and the last one just went in.
		await loadWith({ items: [item('Balsa', 1)] });

		applyActivityEnded(
			ended('CraftBalsaHandle', { items: [item('Balsa', 0), item('BalsaHandle', 10)] })
		);

		expect(gameState.items.Balsa).toBe(0);
		expect(gameState.lastReward?.items).toEqual([
			{ itemId: 'Balsa', delta: -1 },
			{ itemId: 'BalsaHandle', delta: 10 }
		]);
		expect(gameState.running).toBeNull();
	});

	it('keeps the activity going while its costs are covered', async () => {
		await loadWith({ items: [item('Balsa', 2)] });

		applyActivityEnded(ended('CraftBalsaHandle', { items: [item('Balsa', 1)] }));

		expect(gameState.running?.activityId).toBe('CraftBalsaHandle');
	});

	it('leaves the running activity to the start that is still in flight', async () => {
		await loadWith({ skills: [skill('Mining', 40000, 16)] });
		applyActivityEnded(ended('MineTin'));
		let land = (): void => {};
		request.mockImplementation((type: string) =>
			type === 'StartActivityRequest'
				? new Promise((resolve) => (land = () => resolve(responseTo(type, {}))))
				: Promise.resolve(responseTo(type, {}))
		);

		const starting = startActivity('MineCopper');
		// The stop has landed; the start is still on its way over.
		await vi.waitUntil(() => gameState.running === null);

		// The cycle the server was already running completes in that window, and
		// its event still names the activity being replaced.
		applyActivityEnded(ended('MineTin', { items: [item('Tin', 2)] }));

		expect(gameState.items.Tin).toBe(2);
		// Not resurrected: the request in flight is the newer answer.
		expect(gameState.running).toBeNull();

		land();
		await starting;
		expect(gameState.running?.activityId).toBe('MineCopper');
	});

	it('wins over a load it lands in the middle of', async () => {
		const releases: (() => void)[] = [];
		request.mockImplementation(
			(type: string) =>
				new Promise((resolve) =>
					releases.push(() =>
						resolve(
							responseTo(type, {
								skills: [skill('Mining', 200, 1), skill('Crafting', 0, 1)],
								items: [item('Tin', 3), item('Copper', 1)]
							})
						)
					)
				)
		);

		const loading = loadGame();
		// The payout committed after the load's database read: newer for its rows.
		applyActivityEnded(
			ended('MineTin', { skills: [skill('Mining', 400, 1)], items: [item('Tin', 5)] })
		);
		for (const release of releases) {
			release();
		}
		await loading;

		expect(gameState.skills.Mining?.xp).toBe(400);
		expect(gameState.skills.Crafting?.xp).toBe(0);
		expect(gameState.items).toEqual({ Tin: 5, Copper: 1 });
		expect(gameState.status).toBe('loaded');
	});
});

describe('startActivity', () => {
	it('sends only the start when nothing is running', async () => {
		respond();

		await expect(startActivity('MineTin')).resolves.toBe(true);

		expect(request).toHaveBeenCalledTimes(1);
		expect(request).toHaveBeenCalledWith('StartActivityRequest', { activityId: 'MineTin' });
		expect(gameState.running?.activityId).toBe('MineTin');
		expect(gameState.pending).toBeNull();
		expect(gameState.actionError).toBeNull();
	});

	it('stops the running activity before starting the next', async () => {
		respond();
		applyActivityEnded(ended('MineTin'));

		await startActivity('MineCopper');

		expect(request).toHaveBeenNthCalledWith(1, 'StopActivityRequest', {});
		expect(request).toHaveBeenNthCalledWith(2, 'StartActivityRequest', {
			activityId: 'MineCopper'
		});
		expect(gameState.running?.activityId).toBe('MineCopper');
	});

	it('treats a refusal to stop nothing as stopped', async () => {
		respondOrRefuse({ StopActivityRequest: [NOT_DOING] });
		applyActivityEnded(ended('MineTin'));

		await expect(startActivity('MineCopper')).resolves.toBe(true);

		expect(gameState.running?.activityId).toBe('MineCopper');
		expect(gameState.actionError).toBeNull();
	});

	it('takes over an activity the socket was already doing', async () => {
		// After a reload the store knows nothing, but the socket is still mining.
		respondOrRefuse({ StartActivityRequest: [ALREADY_DOING, null] });

		await expect(startActivity('MineCopper')).resolves.toBe(true);

		expect(request).toHaveBeenNthCalledWith(1, 'StartActivityRequest', {
			activityId: 'MineCopper'
		});
		expect(request).toHaveBeenNthCalledWith(2, 'StopActivityRequest', {});
		expect(request).toHaveBeenNthCalledWith(3, 'StartActivityRequest', {
			activityId: 'MineCopper'
		});
		expect(gameState.running?.activityId).toBe('MineCopper');
	});

	it('surfaces a second refusal rather than looping', async () => {
		respondOrRefuse({ StartActivityRequest: [ALREADY_DOING, ALREADY_DOING] });

		await expect(startActivity('MineCopper')).resolves.toBe(false);

		expect(request).toHaveBeenCalledTimes(3);
		expect(gameState.actionError).toBe(ALREADY_DOING);
		expect(gameState.running).toBeNull();
	});

	it('surfaces a level refusal verbatim', async () => {
		request.mockRejectedValue(new Error("Activity 'MineCopper' requires Mining level 11."));

		await expect(startActivity('MineCopper')).resolves.toBe(false);

		expect(gameState.actionError).toBe("Activity 'MineCopper' requires Mining level 11.");
		expect(gameState.running).toBeNull();
		expect(gameState.pending).toBeNull();
	});

	it('drops the activity it stopped when the start meant to replace it fails', async () => {
		// Level 16 so the store's own check lets this one through to the socket.
		await loadWith({ skills: [skill('Mining', 40000, 16)] });
		applyActivityEnded(ended('MineTin'));
		respondOrRefuse({ StartActivityRequest: ['Internal server error.'] });

		await expect(startActivity('MineCopper')).resolves.toBe(false);

		expect(request).toHaveBeenCalledWith('StopActivityRequest', {});
		expect(gameState.actionError).toBe('Internal server error.');
		// The stop landed, so nothing is running — least of all the activity the
		// board would otherwise still be sweeping a meter for.
		expect(gameState.running).toBeNull();
	});

	it('refuses a start below the level without spending the running activity on it', async () => {
		await loadWith({ skills: [skill('Mining', 900, 2)] });
		applyActivityEnded(ended('MineTin'));
		request.mockClear();

		await expect(startActivity('MineCopper')).resolves.toBe(false);

		expect(request).not.toHaveBeenCalled();
		expect(gameState.actionError).toBe("Activity 'MineCopper' requires Mining level 11.");
		expect(gameState.running?.activityId).toBe('MineTin');
	});

	it('refuses a start its pack cannot cover, wording it as the backend would', async () => {
		await loadWith({});
		request.mockClear();

		await expect(startActivity('CraftBalsaHandle')).resolves.toBe(false);

		expect(request).not.toHaveBeenCalled();
		expect(gameState.actionError).toBe("Activity 'CraftBalsaHandle' requires 1 of Balsa.");
	});

	it('asks the socket rather than guessing before the board has loaded', async () => {
		// Nothing has been loaded, so the store knows no levels and no items — and
		// must not read that emptiness as a profile that cannot mine copper.
		respond();

		await expect(startActivity('MineCopper')).resolves.toBe(true);

		expect(request).toHaveBeenCalledWith('StartActivityRequest', { activityId: 'MineCopper' });
	});

	it('ignores a press on the action already running', async () => {
		applyActivityEnded(ended('MineTin'));

		await expect(startActivity('MineTin')).resolves.toBe(false);

		expect(request).not.toHaveBeenCalled();
	});

	it('ignores a second start while one is in flight', async () => {
		let release = (): void => {};
		request.mockReturnValueOnce(new Promise((resolve) => (release = () => resolve(undefined))));

		const first = startActivity('MineTin');
		await expect(startActivity('MineCopper')).resolves.toBe(false);

		expect(request).toHaveBeenCalledTimes(1);
		expect(gameState.pending).toBe('MineTin');
		release();
		await first;
		expect(gameState.pending).toBeNull();
	});
});

describe('stopActivity', () => {
	it('stops and forgets the running activity', async () => {
		respond();
		applyActivityEnded(ended('MineTin'));

		await expect(stopActivity()).resolves.toBe(true);

		expect(request).toHaveBeenCalledWith('StopActivityRequest', {});
		expect(gameState.running).toBeNull();
		expect(gameState.pending).toBeNull();
	});

	it('treats nothing running as stopped', async () => {
		request.mockRejectedValue(new Error(NOT_DOING));
		applyActivityEnded(ended('MineTin'));

		await expect(stopActivity()).resolves.toBe(true);

		expect(gameState.running).toBeNull();
		expect(gameState.actionError).toBeNull();
	});

	it('surfaces any other refusal and keeps the activity', async () => {
		request.mockRejectedValue(new Error('Internal server error.'));
		applyActivityEnded(ended('MineTin'));

		await expect(stopActivity()).resolves.toBe(false);

		expect(gameState.actionError).toBe('Internal server error.');
		expect(gameState.running?.activityId).toBe('MineTin');
	});
});

/*
 * See profiles.spec.ts: the session reset runs before a dead connection's
 * rejection reaches the store, so nothing may be written on either outcome.
 */
describe('a connection dropping under a request', () => {
	function dropDuring<T>(promise: Promise<T>): Promise<T> {
		resetSessionState();
		connection.generation++;
		return promise;
	}

	it('leaves the board loadable rather than stuck on an error', async () => {
		let fail = (): void => {};
		request.mockImplementation(
			() => new Promise((_, reject) => (fail = () => reject(new Error('WebSocket closed'))))
		);

		const loading = loadGame();
		expect(gameState.status).toBe('loading');

		const dropped = dropDuring(loading);
		fail();
		await dropped;

		expect(gameState.status).toBe('idle');
		expect(gameState.error).toBeNull();

		respond({ items: [item('Tin', 1)] });
		await loadGame();
		expect(gameState.status).toBe('loaded');
		expect(gameState.items).toEqual({ Tin: 1 });
	});

	it('does not surface a dead connection failure on a start', async () => {
		let fail = (): void => {};
		request.mockReturnValueOnce(
			new Promise((_, reject) => (fail = () => reject(new Error('WebSocket closed'))))
		);

		const starting = startActivity('MineTin');
		const dropped = dropDuring(starting);
		fail();

		await expect(dropped).resolves.toBe(false);
		expect(gameState.actionError).toBeNull();
		expect(gameState.pending).toBeNull();
		expect(gameState.running).toBeNull();
	});

	it('discards a start that succeeds after its connection died', async () => {
		let land = (): void => {};
		request.mockReturnValueOnce(new Promise((resolve) => (land = () => resolve(undefined))));

		const starting = startActivity('MineTin');
		const dropped = dropDuring(starting);
		land();

		await expect(dropped).resolves.toBe(false);
		expect(gameState.running).toBeNull();
	});
});
