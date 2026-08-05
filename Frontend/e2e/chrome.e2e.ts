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

test('logging in surfaces the failure when the socket will not open', async ({ page }) => {
	await page.routeWebSocket(WS_ROUTE, (ws) => ws.close());

	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();

	await expect(page.getByText('error', { exact: true })).toBeVisible();
});
