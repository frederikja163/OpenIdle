import { expect, test } from '@playwright/test';

// Runs against `bun run build && bun run preview` (see playwright.config.ts), so
// this is the production build rather than the dev server — which is the whole
// reason Playwright is carried alongside Vitest.
//
// No backend runs here. /login does not log in on its own, so an untouched visit
// rests at 'loggedOut'; it is pressing the button that fails, which the last
// test drives.
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

test('logging in surfaces the failure when no backend is reachable', async ({ page }) => {
	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();

	await expect(page.getByText('error', { exact: true })).toBeVisible();
});
