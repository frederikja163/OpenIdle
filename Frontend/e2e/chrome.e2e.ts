import { expect, test, type WebSocketRoute } from '@playwright/test';

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

const THORIN = { name: 'Thorin', profileId: 'p1' };

/**
 * The whole backend, for tests that only need the socket to say yes: every
 * request gets the response named after it, and ListProfiles gets a list.
 */
function respondToRequests(
	ws: WebSocketRoute,
	profiles: (typeof THORIN)[]
): (frame: string | Buffer) => void {
	return (frame) => {
		const { $type, requestId } = JSON.parse(String(frame));
		if ($type === 'ListProfilesRequest') {
			ws.send(JSON.stringify({ $type: 'ListProfilesResponse', requestId, profiles }));
			return;
		}
		ws.send(JSON.stringify({ $type: `${$type.replace('Request', '')}Response`, requestId }));
	};
}

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
			const { requestId } = JSON.parse(String(frame));
			ws.send(JSON.stringify({ $type: 'LoginAsTestUserResponse', requestId }));
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

test('a dropped socket reconnects and replays the session', async ({ page }) => {
	const sentPerConnection: string[][] = [];
	let dropTheFirstSocket = (): void => {};

	await page.routeWebSocket(WS_ROUTE, (ws) => {
		const connection = sentPerConnection.push([]) - 1;
		if (connection === 0) {
			dropTheFirstSocket = () => ws.close();
		}
		const respond = respondToRequests(ws, [THORIN]);
		ws.onMessage((frame) => {
			sentPerConnection[connection].push(JSON.parse(String(frame)).$type);
			respond(frame);
		});
	});

	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);
	await expect(page.getByText('Thorin')).toBeVisible();

	// Take the profile into the game, so there is a selection to put back.
	await page.getByRole('button', { name: 'Load' }).click();
	await expect(page).toHaveURL(/\/game$/);
	await page.goBack();
	await expect(page).toHaveURL(/\/profiles$/);

	dropTheFirstSocket();

	// The guard must hold position: bouncing to /login on a recoverable drop is
	// what made every blip cost a manual login.
	await expect(page.getByRole('status')).toHaveText('Reconnecting…');
	await expect(page).toHaveURL(/\/profiles$/);

	// ...and the list comes back on its own, with no user action at all.
	await expect(page.getByText('Thorin')).toBeVisible();
	expect(sentPerConnection).toHaveLength(2);
	// Order is the point: the socket has to be logged in before it can be
	// pointed at a profile, and the refetch rides behind both.
	expect(sentPerConnection[1]).toEqual([
		'LoginAsTestUserRequest',
		'SelectProfileRequest',
		'ListProfilesRequest'
	]);
});

test('deleting a profile asks first, and confirming does nothing yet', async ({ page }) => {
	await page.routeWebSocket(WS_ROUTE, (ws) => ws.onMessage(respondToRequests(ws, [THORIN])));

	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	// Scoped to the card rather than the page: the dialog puts a second Delete
	// button in the document, and a third would arrive with a second profile. The
	// panel carries no role of its own, so its data-slot is the handle.
	const card = page.locator('[data-slot="card"]', { hasText: 'Thorin' });
	const trigger = card.getByRole('button', { name: 'Delete' });
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

	// TODO: confirming is inert only because no delete message exists on the wire
	// yet. When the backend branch lands, wire the request and change the last
	// assertion below to expect the card to disappear.
	await trigger.click();
	await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
	await expect(page.getByRole('dialog')).toBeHidden();
	await expect(page.getByText('Thorin')).toBeVisible();
});

// The ?ws= override is what lets the one deployed dev frontend be pointed at
// whichever backend a developer is running locally — something no single
// PUBLIC_WS_URL can do, since every developer's localhost is their own. These
// run under PUBLIC_ALLOW_WS_OVERRIDE=true (see playwright.config.ts), which is
// the deployed dev frontend's configuration and not production's.

const LOCAL_BACKEND = 'ws://127.0.0.1:5066/ws';
const OTHER_BACKEND = 'ws://127.0.0.1:9999/ws';

/**
 * Records which address the client actually dialled. The route pattern has to
 * be broad enough to catch an overridden port, so it matches any /ws.
 */
async function captureDialledUrl(page: import('@playwright/test').Page): Promise<string[]> {
	const dialled: string[] = [];
	await page.routeWebSocket(WS_ROUTE, (ws) => {
		dialled.push(ws.url());
		ws.onMessage(respondToRequests(ws, [THORIN]));
	});
	return dialled;
}

test('a ?ws= override points the socket at another backend', async ({ page }) => {
	const dialled = await captureDialledUrl(page);

	await page.goto(`/login?ws=${encodeURIComponent(OTHER_BACKEND)}`);
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	expect(dialled).toEqual([OTHER_BACKEND]);
});

test('an override survives a reload, so it need only be typed once', async ({ page }) => {
	const dialled = await captureDialledUrl(page);

	await page.goto(`/login?ws=${encodeURIComponent(OTHER_BACKEND)}`);
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	// Navigating to a plain URL, with no parameter to re-supply it.
	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	expect(dialled).toEqual([OTHER_BACKEND, OTHER_BACKEND]);
});

test('an empty ?ws= hands the client back to its own backend', async ({ page }) => {
	const dialled = await captureDialledUrl(page);

	await page.goto(`/login?ws=${encodeURIComponent(OTHER_BACKEND)}`);
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	await page.goto('/login?ws=');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	expect(dialled).toEqual([OTHER_BACKEND, LOCAL_BACKEND]);
});

test('a malformed override is ignored rather than breaking the client', async ({ page }) => {
	const dialled = await captureDialledUrl(page);

	await page.goto('/login?ws=not-a-url');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);

	expect(dialled).toEqual([LOCAL_BACKEND]);
});
