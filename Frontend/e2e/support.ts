import { expect, type Page, type WebSocketRoute } from '@playwright/test';

/*
 * What every suite here needs before it can assert anything: the socket route to
 * stub, the shape of a stubbed backend's replies, and the login that has to
 * happen in the browser rather than by navigating straight to a guarded route.
 *
 * Playwright collects `*.e2e.ts` only, so this file is a module and not a suite.
 */

// Matches the URL itself rather than a glob, which Playwright would resolve
// against baseURL and so never match a socket on another port.
export const WS_ROUTE = /\/ws$/;

// /login carries a redirectTo query param whenever the (auth) guard bounced a
// protected route there, so assert on the pathname plus optional query.
export const LOGIN_URL = /\/login(\?.*)?$/;

export interface StubProfile {
	name: string;
	profileId: string;
}

export const THORIN: StubProfile = { name: 'Thorin', profileId: 'p1' };

/** The response named after the request, which is all most frames need. */
export function bareResponse(ws: WebSocketRoute, type: string, requestId: number): void {
	ws.send(JSON.stringify({ $type: `${type.replace('Request', '')}Response`, requestId }));
}

/**
 * The whole backend, for tests that only need the socket to say yes: every
 * request gets the response named after it, and ListProfiles gets a list.
 */
export function respondToRequests(
	ws: WebSocketRoute,
	profiles: StubProfile[]
): (frame: string | Buffer) => void {
	return (frame) => {
		const { $type, requestId } = JSON.parse(String(frame));
		if ($type === 'ListProfilesRequest') {
			ws.send(JSON.stringify({ $type: 'ListProfilesResponse', requestId, profiles }));
			return;
		}
		bareResponse(ws, $type, requestId);
	};
}

/**
 * Signs in and lands on /profiles. The login lives in client state, so a test
 * that needs a session has to go through the form: navigating straight to a
 * guarded route reloads the document, drops that state and bounces to /login.
 */
export async function logIn(page: Page): Promise<void> {
	await page.goto('/login');
	await page.getByRole('button', { name: 'Log in' }).click();
	await expect(page).toHaveURL(/\/profiles$/);
}
