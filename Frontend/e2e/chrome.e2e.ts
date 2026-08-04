import { expect, test } from '@playwright/test';

// Runs against `bun run build && bun run preview` (see playwright.config.ts), so
// this is the production build rather than the dev server — which is the whole
// reason Playwright is carried alongside Vitest.
test('the root route redirects to /profiles and renders the chrome', async ({ page }) => {
	await page.goto('/');

	await expect(page).toHaveURL(/\/profiles$/);
	await expect(page.getByRole('heading', { level: 1, name: 'Profiles' })).toBeVisible();
	await expect(page.getByRole('link', { name: 'Profiles' })).toHaveAttribute(
		'aria-current',
		'page'
	);
});
