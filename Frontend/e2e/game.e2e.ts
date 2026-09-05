import { expect, test, type Page, type WebSocketRoute } from '@playwright/test';

// The board is driven by the game protocol, so the socket is stubbed with a
// small backend that answers the loads from a fixture world, says yes to every
// start and stop, and can push a payout. What is worth pinning is the wiring:
// that a click becomes the right request, that a payout lands on the skill and
// the pack, and that the locks the backend would enforce refuse the click here.
//
// See the note in chrome.e2e.ts on why the socket is stubbed by URL regex.
const WS_ROUTE = /\/ws$/;

const THORIN = { name: 'Thorin', profileId: 'p1' };

interface World {
	skills: { skillId: string; xp: number; level: number }[];
	items: { itemId: string; count: number }[];
}

// Mining sits 5 xp into level 2 (895 opens it; level 3 is 1011 further), the
// other skills are fresh, and the pack is empty.
const FRESH_WORLD: World = {
	skills: [
		{ skillId: 'Mining', xp: 900, level: 2 },
		{ skillId: 'LumberJacking', xp: 0, level: 1 },
		{ skillId: 'Crafting', xp: 0, level: 1 }
	],
	items: []
};

interface Backend {
	started: string[];
	stops: number;
	/** The socket the page is on, for pushing events. */
	socket: () => WebSocketRoute;
}

function withProfile<T extends object>(rows: T[]): (T & { profileId: string })[] {
	return rows.map((row) => ({ profileId: THORIN.profileId, ...row }));
}

/** Answers a frame: the loads from `world`, a bare response for everything else. */
function respond(ws: WebSocketRoute, world: World, backend: Backend, refuseStart?: string) {
	return (frame: string | Buffer) => {
		const { $type, requestId, activityId } = JSON.parse(String(frame));
		switch ($type) {
			case 'ListProfilesRequest':
				ws.send(JSON.stringify({ $type: 'ListProfilesResponse', requestId, profiles: [THORIN] }));
				return;
			case 'GetSkillsRequest':
				ws.send(
					JSON.stringify({
						$type: 'GetSkillsResponse',
						requestId,
						skills: withProfile(world.skills)
					})
				);
				return;
			case 'GetItemsRequest':
				ws.send(
					JSON.stringify({ $type: 'GetItemsResponse', requestId, items: withProfile(world.items) })
				);
				return;
			case 'StartActivityRequest':
				backend.started.push(activityId);
				if (refuseStart) {
					ws.send(JSON.stringify({ $type: 'ErrorResponse', requestId, message: refuseStart }));
					return;
				}
				break;
			case 'StopActivityRequest':
				backend.stops++;
				break;
		}
		ws.send(JSON.stringify({ $type: `${$type.replace('Request', '')}Response`, requestId }));
	};
}

/** Pushes one completed cycle: the touched rows with their new absolute totals. */
function payout(ws: WebSocketRoute, activityId: string, world: World): void {
	ws.send(
		JSON.stringify({
			$type: 'ActivityEndedEvent',
			eventId: 0,
			timestamp: Date.now(),
			activityId,
			skills: withProfile(world.skills),
			items: withProfile(world.items)
		})
	);
}

// The login lives in client state, so the board has to be reached by navigating
// within the app. A `goto('/game')` would reload the document, drop that state
// and bounce straight back out through the auth guard.
async function logIn(page: Page, world: World, refuseStart?: string): Promise<Backend> {
	let socket: WebSocketRoute | undefined;
	const backend: Backend = { started: [], stops: 0, socket: () => socket! };
	await page.routeWebSocket(WS_ROUTE, (ws) => {
		socket = ws;
		ws.onMessage(respond(ws, world, backend, refuseStart));
	});
	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);
	return backend;
}

/** Logs in and loads the profile, which is what puts the socket on it. */
async function openBoard(page: Page, world = FRESH_WORLD, refuseStart?: string): Promise<Backend> {
	const backend = await logIn(page, world, refuseStart);
	await page.getByRole('button', { name: 'Load' }).click();
	await expect(page).toHaveURL(/\/game$/);
	await expect(page.getByRole('heading', { name: 'Skills' })).toBeVisible();
	return backend;
}

// Both stop controls carry the same name, so each is scoped to what owns it:
// the panel header for one, the running card's own wrapper for the other.
function headerStop(page: Page) {
	return page
		.getByRole('heading', { name: 'Skills' })
		.locator('..')
		.getByRole('button', { name: 'Stop Mine Tin Ore' });
}

function tinCard(page: Page) {
	return page.getByRole('button', { name: /^×2 Mine Tin Ore/ });
}

function cardStop(page: Page) {
	return tinCard(page).locator('..').getByRole('button', { name: 'Stop Mine Tin Ore' });
}

test('the board fills the viewport without scrolling the document', async ({ page }) => {
	await page.setViewportSize({ width: 1280, height: 800 });
	await openBoard(page);

	await expect(page.getByRole('heading', { name: 'Inventory' })).toBeVisible();
	await expect(page.getByRole('heading', { name: 'Skills' })).toBeVisible();

	const overflow = await page.evaluate(
		() => document.documentElement.scrollHeight - window.innerHeight
	);
	expect(overflow).toBeLessThanOrEqual(0);
});

test('the game link without a loaded profile asks for one', async ({ page }) => {
	await logIn(page, FRESH_WORLD);

	await page.getByRole('link', { name: 'Game' }).click();
	await expect(page).toHaveURL(/\/game$/);

	await expect(page.getByText('No profile selected')).toBeVisible();
	await page.getByRole('link', { name: 'Choose a profile' }).click();
	await expect(page).toHaveURL(/\/profiles$/);
});

test('the loaded profile dresses the rail and the pack', async ({ page }) => {
	await openBoard(page, {
		...FRESH_WORLD,
		items: [{ itemId: 'Tin', count: 3 }]
	});

	await expect(page.getByText('5/1011 XP')).toBeVisible();
	await expect(page.getByRole('button', { name: 'Tin Ore · 3' })).toBeVisible();
	await expect(headerStop(page)).toHaveCount(0);
});

test('starting an action asks the backend and its payout lands on the board', async ({ page }) => {
	const backend = await openBoard(page);

	await tinCard(page).click();

	await expect(headerStop(page)).toBeVisible();
	expect(backend.started).toEqual(['MineTin']);
	expect(backend.stops).toBe(0);

	payout(backend.socket(), 'MineTin', {
		skills: [{ skillId: 'Mining', xp: 1100, level: 2 }],
		items: [{ itemId: 'Tin', count: 2 }]
	});

	await expect(page.getByRole('button', { name: 'Tin Ore · 2' })).toBeVisible();
	await expect(page.getByText('205/1011 XP')).toBeVisible();
	await expect(page.getByText('+200 XP')).toBeVisible();
	await expect(page.getByText('+2', { exact: true })).toBeVisible();
});

test('the card stop button appears on hover and stops the action', async ({ page }) => {
	const backend = await openBoard(page);
	await tinCard(page).click();
	await expect(headerStop(page)).toBeVisible();
	// The click left the pointer on the card, which is the hover under test.
	await page.mouse.move(0, 0);

	const stop = cardStop(page);

	// Playwright counts an opacity-0 element as visible, so the reveal itself is
	// what has to be asserted rather than visibility.
	await expect(stop).toHaveCSS('opacity', '0');
	await tinCard(page).hover();
	await expect(stop).toHaveCSS('opacity', '1');

	// The reward float shares this corner and outlives a whole tick, so if it
	// ever takes the pointer it steals the hover the button hangs on: hold the
	// cursor on the button across a payout and it has to stay revealed.
	await stop.hover();
	payout(backend.socket(), 'MineTin', {
		skills: [{ skillId: 'Mining', xp: 1100, level: 2 }],
		items: [{ itemId: 'Tin', count: 2 }]
	});
	await expect(page.getByText('+200 XP')).toBeVisible();
	await expect(stop).toHaveCSS('opacity', '1');

	await stop.click();
	await expect(stop).toHaveCount(0);
	await expect(headerStop(page)).toHaveCount(0);
	expect(backend.stops).toBe(1);
});

test('the header stop button stops the action', async ({ page }) => {
	const backend = await openBoard(page);
	await tinCard(page).click();

	await headerStop(page).click();

	await expect(headerStop(page)).toHaveCount(0);
	await expect(cardStop(page)).toHaveCount(0);
	expect(backend.stops).toBe(1);
});

test('switching actions stops the running one first', async ({ page }) => {
	const backend = await openBoard(page, {
		...FRESH_WORLD,
		skills: [{ skillId: 'Mining', xp: 40000, level: 16 }]
	});
	await tinCard(page).click();
	await expect(headerStop(page)).toBeVisible();

	await page.getByRole('button', { name: /^×2 Mine Copper Ore/ }).click();

	await expect(
		page
			.getByRole('heading', { name: 'Skills' })
			.locator('..')
			.getByRole('button', { name: 'Stop Mine Copper Ore' })
	).toBeVisible();
	expect(backend.stops).toBe(1);
	expect(backend.started).toEqual(['MineTin', 'MineCopper']);
});

test('a level-locked action refuses the click', async ({ page }) => {
	await openBoard(page);

	// Copper unlocks at 11 and Mining starts at 2.
	const copper = page.getByRole('button', { name: /Mine Copper Ore/ });
	await expect(copper).toBeDisabled();
	await expect(copper).toContainText('Unlocks at level 11');
});

test('an action short on materials refuses the click', async ({ page }) => {
	await openBoard(page);

	await page.getByRole('button', { name: 'Crafting' }).click();

	// A balsa handle costs one balsa log and the pack is empty.
	const handle = page.getByRole('button', { name: /Craft Balsa Handle/ });
	await expect(handle).toBeDisabled();
	await expect(handle).toContainText('Missing materials');
});

test('a refused start is reported on the board', async ({ page }) => {
	await openBoard(page, FRESH_WORLD, "Activity 'MineTin' requires Mining level 1.");

	await tinCard(page).click();

	await expect(page.getByRole('alert')).toHaveText("Activity 'MineTin' requires Mining level 1.");
	await expect(headerStop(page)).toHaveCount(0);
});
