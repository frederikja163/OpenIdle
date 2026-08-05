import { expect, test } from '@playwright/test';

// Runs against `bun run build && bun run preview` (see playwright.config.ts), so
// this is the production build rather than the dev server — which is the whole
// reason Playwright is carried alongside Vitest.
//
// PUBLIC_WS_URL defaults to the address the backend binds in development, so a
// test that needs the socket to behave a certain way stubs it with
// routeWebSocket rather than assuming nothing is listening. Tests that never log
// in need no stub: the client is constructed at import time but only connects
// once a request is sent.

// Matches the URL itself rather than a glob, which Playwright would resolve
// against baseURL and so never match a socket on another port.
const WS_ROUTE = /\/ws$/;

test('the root route funnels through the auth guards to /login', async ({ page }) => {
	await page.goto('/');

	await expect(page).toHaveURL(/\/login$/);
	await expect(page.getByRole('heading', { level: 1, name: 'Login' })).toBeVisible();
	await expect(page.getByText('loggedOut', { exact: true })).toBeVisible();
	await expect(page.getByRole('button', { name: 'Log in' })).toBeVisible();
});

test('a protected route bounces to /login when logged out', async ({ page }) => {
	await page.goto('/game');

	await expect(page).toHaveURL(/\/login$/);
});

test('the guard replaces the rejected entry instead of stacking one', async ({ page }) => {
	await page.goto('/login');
	const entries = await page.evaluate(() => history.length);

	await page.goto('/game');
	await expect(page).toHaveURL(/\/login$/);

	// One entry for the /game navigation and none for the bounce, so Back still
	// leads out of the app instead of to the route the guard just rejected.
	expect(await page.evaluate(() => history.length)).toBe(entries + 1);
});

test('logging in surfaces the failure when the socket will not open', async ({ page }) => {
	await page.routeWebSocket(WS_ROUTE, (ws) => ws.close());

	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();

	await expect(page.getByText('error', { exact: true })).toBeVisible();
});

test('a successful login replaces /login rather than stacking /profiles on it', async ({
	page
}) => {
	await page.routeWebSocket(WS_ROUTE, (ws) => {
		// connectToServer() is never called, so these frames are the whole
		// backend as far as this test is concerned.
		ws.onMessage((frame) => {
			const { Id } = JSON.parse(String(frame));
			ws.send(JSON.stringify({ $type: 'LoginAsTestUserResponse', Id }));
		});
	});

	await page.goto('/login');
	const entries = await page.evaluate(() => history.length);
	await page.getByRole('button', { name: 'Log in' }).click();

	await expect(page).toHaveURL(/\/profiles$/);
	expect(await page.evaluate(() => history.length)).toBe(entries);
});
