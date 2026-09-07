/** Which commit a build came from; nulls mean it was built outside CI. */
export interface BuildInfo {
	readonly commit: string | null;
	/** Committer date as epoch milliseconds, the wire format for timestamps. */
	readonly commitTime: number | null;
}

const SHORT_SHA_LENGTH = 7;

function pad(value: number): string {
	return String(value).padStart(2, '0');
}

/** `YYYY-MM-DD HH:MM:SS` in UTC, so every viewer quotes the same string for a build. */
export function formatUtc(epochMs: number): string {
	const date = new Date(epochMs);
	return (
		`${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())} ` +
		`${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}:${pad(date.getUTCSeconds())}`
	);
}

/**
 * `2026-09-04 23:26:40 1e1c256` for a CI build, `local` for anything else. A
 * commit without a date is shown on its own rather than with a made-up time.
 */
export function formatVersion(build: BuildInfo): string {
	if (!build.commit) {
		return 'local';
	}
	const sha = build.commit.slice(0, SHORT_SHA_LENGTH);
	return build.commitTime === null ? sha : `${formatUtc(build.commitTime)} ${sha}`;
}
