import { json } from '@sveltejs/kit';

/**
 * The build this image was made from, in the same shape as the backend's
 * GET /version, so ops and CI can ask either image which commit it runs. The
 * values are the ones vite.config.ts inlines into the bundle, which is also
 * what the version footer shows for the frontend half.
 *
 * Like /health it says nothing about the backend: the frontend's build is its
 * own, and the backend it points at answers for itself.
 */
export function GET() {
	return json(__OPENIDLE_BUILD__);
}
