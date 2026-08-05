import { expect, test } from '@playwright/test';

// The board is mock-driven and simulates its own idle loop, so what is worth
// pinning here is the wiring the mock data cannot vouch for on its own: that the
// board fits the viewport without scrolling the document, and that the loop
// actually runs and pays out.
//
// See the note in chrome.e2e.ts on why the socket is stubbed by URL regex.
const WS_ROUTE = /\/ws$/;

// The login lives in client state, so the board has to be reached by navigating
// within the app. A `goto('/game')` would reload the document, drop that state
// and bounce straight back out through the auth guard.
async function openBoard(page: import('@playwright/test').Page) {
	await page.routeWebSocket(WS_ROUTE, (ws) => {
		ws.onMessage((frame) => {
			const { Id } = JSON.parse(String(frame));
			ws.send(JSON.stringify({ $type: 'LoginAsTestUserResponse', Id }));
		});
	});
	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	await page.getByRole('link', { name: 'Game' }).click();
	await expect(page).toHaveURL(/\/game$/);
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

test('the running action pays out into the skill and the inventory', async ({ page }) => {
	await openBoard(page);

	// Mining/Talc starts already running, and one completion takes 5s.
	const talc = page.getByRole('button', { name: /^Talc Ore/ });
	await expect(talc).toHaveAccessibleName('Talc Ore · 18');
	await expect(page.getByText('1 running')).toBeVisible();

	await expect(talc).toHaveAccessibleName('Talc Ore · 19', { timeout: 8000 });
	await expect(page.getByText('65/100 XP')).toBeVisible();
});

test('clicking the running action stops the loop', async ({ page }) => {
	await openBoard(page);

	await page.getByRole('button', { name: /^×1 Mine Talc Ore/ }).click();

	// Exact, or it also matches the "OpenIdle" wordmark in the chrome.
	await expect(page.getByText('Idle', { exact: true })).toBeVisible();
	await expect(page.getByText('1 running')).toHaveCount(0);
});

test('a level-locked action refuses the click', async ({ page }) => {
	await openBoard(page);

	// Apatite unlocks at 12 and Mining starts at 7.
	const apatite = page.getByRole('button', { name: /Mine Apatite Ore/ });
	await expect(apatite).toBeDisabled();
	await expect(apatite).toContainText('Unlocks at level 12');
});

test('an action short on materials refuses the click', async ({ page }) => {
	await openBoard(page);

	await page.getByRole('button', { name: 'Crafting' }).click();

	// Calcite Pickaxe Head costs 6 calcite and the board starts with 4.
	const calcitepick = page.getByRole('button', { name: /Craft Calcite Pickaxe Head/ });
	await expect(calcitepick).toBeDisabled();
	await expect(calcitepick).toContainText('Missing materials');
});
