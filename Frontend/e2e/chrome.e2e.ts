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

// /login carries a redirectTo query param whenever the (auth) guard bounced a
// protected route there, so assert on the pathname plus optional query.
const LOGIN_URL = /\/login(\?.*)?$/;

test('the root route funnels through the auth guards to /login', async ({ page }) => {
	await page.goto('/');

	await expect(page).toHaveURL(LOGIN_URL);
	await expect(page.getByRole('heading', { level: 1, name: 'Login' })).toBeVisible();
	await expect(page.getByTestId('login-status')).toHaveText('Signed out');
	await expect(page.getByRole('button', { name: 'Log in' })).toBeVisible();
});

test('a protected route bounces to /login when logged out', async ({ page }) => {
	await page.goto('/game');

	await expect(page).toHaveURL(LOGIN_URL);
});

test('the guard replaces the rejected entry instead of stacking one', async ({ page }) => {
	await page.goto('/login');
	const entries = await page.evaluate(() => history.length);

	await page.goto('/game');
	await expect(page).toHaveURL(LOGIN_URL);

	// One entry for the /game navigation and none for the bounce, so Back still
	// leads out of the app instead of to the route the guard just rejected.
	expect(await page.evaluate(() => history.length)).toBe(entries + 1);
});

test('logging in surfaces the failure when the socket will not open', async ({ page }) => {
	await page.routeWebSocket(WS_ROUTE, (ws) => ws.close());

	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();

	await expect(page.getByTestId('login-status')).toHaveText('Sign-in failed');
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

	// Logging out drops the socket and runs the auth guard again, which carries
	// the rejected /profiles back to /login as redirectTo.
	await page.getByRole('button', { name: 'Log out' }).click();
	await expect(page).toHaveURL(LOGIN_URL);
	await expect(page.getByTestId('login-status')).toHaveText('Signed out');
});

test('deleting a profile asks first, and confirming does nothing yet', async ({ page }) => {
	await page.routeWebSocket(WS_ROUTE, (ws) => {
		ws.onMessage((frame) => {
			const { $type, Id } = JSON.parse(String(frame));
			if ($type === 'ListProfilesRequest') {
				ws.send(
					JSON.stringify({
						$type: 'ListProfilesResponse',
						Id,
						Profiles: [{ Name: 'Thorin', ProfileId: 'p1' }]
					})
				);
				return;
			}
			ws.send(JSON.stringify({ $type: `${$type.replace('Request', '')}Response`, Id }));
		});
	});

	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	const trigger = page.getByRole('button', { name: 'Delete' });
	await trigger.click();

	// The whole point of the vendored dialog is the behaviour underneath it, so
	// assert that rather than just that some markup appeared: it portals out,
	// takes focus onto the safe button, and gives focus back on dismissal.
	const dialog = page.getByRole('dialog');
	await expect(dialog).toBeVisible();
	await expect(dialog.getByText('Delete Thorin?')).toBeVisible();
	await expect(dialog.getByRole('button', { name: 'Cancel' })).toBeFocused();

	await page.keyboard.press('Escape');
	await expect(dialog).toBeHidden();
	await expect(trigger).toBeFocused();

	// Confirming is inert by design — no delete message exists on the wire.
	await trigger.click();
	await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
	await expect(page.getByRole('dialog')).toBeHidden();
	await expect(page.getByText('Thorin')).toBeVisible();
});
