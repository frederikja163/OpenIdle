import { json } from '@sveltejs/kit';

/**
 * Liveness signal for the container healthcheck and the post-deploy check in
 * .github/workflows/publish-images.yml, mirroring the backend's /healthz.
 *
 * Every other route either redirects or renders the app shell, so without this
 * a healthcheck would be asserting on a 307 or on a route name that is free to
 * change. It deliberately says nothing about the backend: the frontend is
 * serving correctly even when the socket endpoint it points at is down, and
 * conflating the two would restart the wrong container.
 */
export function GET() {
	return json({ status: 'ok' });
}
