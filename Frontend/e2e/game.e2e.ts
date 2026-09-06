import { expect, test, type Page, type WebSocketRoute } from '@playwright/test';
import { bareResponse, logIn, type StubProfile, THORIN, WS_ROUTE } from './support';

// The board is driven by the game protocol, so the socket is stubbed with a
// small backend that answers the loads from a fixture world, says yes to every
// start and stop, and can push a payout. What is worth pinning is the wiring:
// that a click becomes the right request, that a payout lands on the skill and
// the pack, and that the locks the backend would enforce refuse the click here.
//
// See the note in chrome.e2e.ts on why the socket is stubbed by URL regex.

const BALIN: StubProfile = { name: 'Balin', profileId: 'p2' };

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

function withProfile<T extends object>(
	rows: T[],
	profileId: string
): (T & { profileId: string })[] {
	return rows.map((row) => ({ profileId, ...row }));
}

/**
 * Answers a frame: the loads from whichever profile the socket has been pointed
 * at, a bare response for everything else. One world per profile, because a
 * board that keeps showing the previous profile's rows is a bug this suite has
 * to be able to see.
 */
function respond(
	ws: WebSocketRoute,
	worlds: Record<string, World>,
	profiles: StubProfile[],
	backend: Backend,
	refuseStart?: string
) {
	let selected = profiles[0].profileId;
	return (frame: string | Buffer) => {
		const { $type, requestId, activityId, profileId } = JSON.parse(String(frame));
		const world = () => worlds[selected] ?? { skills: [], items: [] };
		switch ($type) {
			case 'ListProfilesRequest':
				ws.send(JSON.stringify({ $type: 'ListProfilesResponse', requestId, profiles }));
				return;
			case 'SelectProfileRequest':
				selected = profileId;
				break;
			case 'GetSkillsRequest':
				ws.send(
					JSON.stringify({
						$type: 'GetSkillsResponse',
						requestId,
						skills: withProfile(world().skills, selected)
					})
				);
				return;
			case 'GetItemsRequest':
				ws.send(
					JSON.stringify({
						$type: 'GetItemsResponse',
						requestId,
						items: withProfile(world().items, selected)
					})
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
		bareResponse(ws, $type, requestId);
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
			skills: withProfile(world.skills, THORIN.profileId),
			items: withProfile(world.items, THORIN.profileId)
		})
	);
}

/** Stubs the socket, then signs in — see ./support.ts for why the form is used. */
async function signIn(
	page: Page,
	worlds: Record<string, World>,
	profiles: StubProfile[],
	refuseStart?: string
): Promise<Backend> {
	let socket: WebSocketRoute | undefined;
	const backend: Backend = { started: [], stops: 0, socket: () => socket! };
	await page.routeWebSocket(WS_ROUTE, (ws) => {
		socket = ws;
		ws.onMessage(respond(ws, worlds, profiles, backend, refuseStart));
	});
	await logIn(page);
	return backend;
}

/** Signs in with one profile, which is all most cases need. */
async function logInOne(page: Page, world = FRESH_WORLD, refuseStart?: string): Promise<Backend> {
	return signIn(page, { [THORIN.profileId]: world }, [THORIN], refuseStart);
}

/** The card for one profile on /profiles, so a two-profile list stays unambiguous. */
function profileCard(page: Page, name: string) {
	return page.locator('[data-slot="card"]', { hasText: name });
}

/** Logs in and loads the profile, which is what puts the socket on it. */
async function openBoard(page: Page, world = FRESH_WORLD, refuseStart?: string): Promise<Backend> {
	const backend = await logInOne(page, world, refuseStart);
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

/** The inventory panel, so its slots are not confused with the action grid. */
function pack(page: Page) {
	return page.locator('[data-slot="card"]', { hasText: 'Inventory' });
}

/** The pack's slots in the order they are drawn, by their accessible names. */
function packOrder(page: Page): Promise<(string | null)[]> {
	return pack(page)
		.getByRole('button', { name: /·/ })
		.evaluateAll((slots) => slots.map((slot) => slot.getAttribute('aria-label')));
}

/** Marks the fill of every meter named `label` that runs a width transition. */
async function watchMeters(page: Page, label: string): Promise<void> {
	await page.evaluate((meterLabel) => {
		for (const meter of document.querySelectorAll(
			`[role="progressbar"][aria-label="${meterLabel}"]`
		)) {
			const fill = meter.firstElementChild;
			if (fill instanceof HTMLElement) {
				fill.addEventListener('transitionstart', (event) => {
					if (event.propertyName === 'width') {
						fill.dataset.widthTransition = 'ran';
					}
				});
			}
		}
	}, label);
}

/*
 * The three below deliberately never measure an element. Anything that reads a
 * layout property — every locator assertion, `innerText`, a bounding box —
 * forces the style recalculation a snap depends on, and a meter that only
 * snapped because the harness happened to force one would pass a test that
 * measured its way through the window under test.
 */
function meterTransitionCount(page: Page): Promise<number> {
	return page.evaluate(() => document.querySelectorAll('[data-width-transition]').length);
}

async function forgetMeterTransitions(page: Page): Promise<void> {
	await page.evaluate(() => {
		for (const fill of document.querySelectorAll<HTMLElement>('[data-width-transition]')) {
			delete fill.dataset.widthTransition;
		}
	});
}

function waitForText(page: Page, text: string): Promise<unknown> {
	return page.waitForFunction(
		(needle) => document.body.textContent?.includes(needle) === true,
		text
	);
}

/** Waits out a style recalculation, so a transition that was going to start has. */
async function settle(page: Page): Promise<void> {
	await page.evaluate(
		() =>
			new Promise<void>((resolve) =>
				requestAnimationFrame(() => requestAnimationFrame(() => resolve()))
			)
	);
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

test('the game link stays dead until a profile is loaded', async ({ page }) => {
	await logInOne(page);

	// Still in the chrome, but not a link: with nothing loaded the board has
	// nothing to show, so there is nowhere for it to lead.
	const gameItem = page.locator('nav').getByText('Game', { exact: true });
	await expect(gameItem).toHaveAttribute('aria-disabled', 'true');
	await expect(gameItem).not.toHaveAttribute('href', /./);
	await expect(page.getByRole('link', { name: 'Game' })).toHaveCount(0);

	await page.getByRole('button', { name: 'Load' }).click();
	await expect(page).toHaveURL(/\/game$/);

	await page.getByRole('link', { name: 'Profiles' }).click();
	await expect(page).toHaveURL(/\/profiles$/);
	await expect(page.getByRole('link', { name: 'Game' })).toBeVisible();
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

test('loading another profile replaces the board rather than keeping the last one', async ({
	page
}) => {
	await signIn(
		page,
		{
			[THORIN.profileId]: { ...FRESH_WORLD, items: [{ itemId: 'Tin', count: 3 }] },
			[BALIN.profileId]: {
				skills: [{ skillId: 'Mining', xp: 0, level: 1 }],
				items: [{ itemId: 'Oak', count: 7 }]
			}
		},
		[THORIN, BALIN]
	);

	await profileCard(page, 'Thorin').getByRole('button', { name: 'Load' }).click();
	await expect(page).toHaveURL(/\/game$/);
	await expect(page.getByRole('button', { name: 'Tin Ore · 3' })).toBeVisible();
	await expect(page.getByText('5/1011 XP')).toBeVisible();

	await page.getByRole('link', { name: 'Profiles' }).click();
	await profileCard(page, 'Balin').getByRole('button', { name: 'Load' }).click();
	await expect(page).toHaveURL(/\/game$/);

	// The second profile's pack and levels, with nothing of the first left over.
	await expect(page.getByRole('button', { name: 'Oak Log · 7' })).toBeVisible();
	await expect(page.getByRole('button', { name: 'Tin Ore · 3' })).toHaveCount(0);
	await expect(page.getByText('0/895 XP')).toBeVisible();
});

test('the pack can be sorted by count and searched by name', async ({ page }) => {
	await openBoard(page, {
		...FRESH_WORLD,
		items: [
			{ itemId: 'Tin', count: 3 },
			{ itemId: 'Copper', count: 1 },
			{ itemId: 'Balsa', count: 5 }
		]
	});

	// Catalog order until something asks for another one.
	expect(await packOrder(page)).toEqual(['Tin Ore · 3', 'Copper Ore · 1', 'Balsa Log · 5']);

	const sort = page.getByRole('button', { name: 'Sort by count' });
	await sort.click();
	await expect(sort).toHaveAttribute('aria-pressed', 'true');
	expect(await packOrder(page)).toEqual(['Balsa Log · 5', 'Tin Ore · 3', 'Copper Ore · 1']);

	await sort.click();
	expect(await packOrder(page)).toEqual(['Tin Ore · 3', 'Copper Ore · 1', 'Balsa Log · 5']);

	await page.getByRole('button', { name: 'Search' }).click();
	const field = page.getByRole('searchbox', { name: 'Search items' });
	await expect(field).toBeFocused();
	await field.fill('ore');
	expect(await packOrder(page)).toEqual(['Tin Ore · 3', 'Copper Ore · 1']);

	await field.fill('nothing here');
	await expect(page.getByText('No items match')).toBeVisible();

	// Closing the field is what clears the query; the whole pack comes back.
	await page.getByRole('button', { name: 'Search' }).click();
	expect(await packOrder(page)).toEqual(['Tin Ore · 3', 'Copper Ore · 1', 'Balsa Log · 5']);
});

// Pins the meter's documented contract — a rise eases, a drop lands at once —
// against the board's own payouts, which is the only place a drop happens. It
// does not distinguish *how* the snap is achieved: an implementation that only
// snapped because something else forced a style recalculation first would pass
// this too, which is why Meter.svelte forces one itself rather than hoping.
test('a level-up snaps the xp meter back instead of rewinding it', async ({ page }) => {
	// Mining sits six xp short of level 3 (1906 opens it), so the payout granting
	// it takes the bar from all but full to nearly empty.
	const backend = await openBoard(page, {
		...FRESH_WORLD,
		skills: [{ skillId: 'Mining', xp: 1900, level: 2 }]
	});
	await expect(page.getByText('1005/1011 XP')).toBeVisible();
	await watchMeters(page, 'Mining experience');

	payout(backend.socket(), 'MineTin', {
		skills: [{ skillId: 'Mining', xp: 1950, level: 3 }],
		items: []
	});

	await waitForText(page, '44/1143 XP');
	await settle(page);
	// A width transition across a drop is the bar rewinding a second's worth of
	// progress the visitor just earned.
	expect(await meterTransitionCount(page)).toBe(0);

	// ...and the transition is still there for the rises it exists for.
	await forgetMeterTransitions(page);
	payout(backend.socket(), 'MineTin', {
		skills: [{ skillId: 'Mining', xp: 2500, level: 3 }],
		items: []
	});

	await waitForText(page, '594/1143 XP');
	await settle(page);
	expect(await meterTransitionCount(page)).toBeGreaterThan(0);
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

test('a start the board can already tell would be refused costs no round trip', async ({
	page
}) => {
	// Mining is level 2 and copper opens at 11, so the card is locked — but the
	// same refusal has to hold for a start that reaches the store another way.
	const backend = await openBoard(page);

	await tinCard(page).click();
	await expect(headerStop(page)).toBeVisible();

	// The locked card refuses the click outright, so the running action survives.
	await page.getByRole('button', { name: /Mine Copper Ore/ }).click({ force: true });

	await expect(headerStop(page)).toBeVisible();
	expect(backend.started).toEqual(['MineTin']);
	expect(backend.stops).toBe(0);
});
