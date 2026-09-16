// Wire values as display text: the two timestamp formats the protocol's
// `TimeStamp` needs, and the casing fix every contract name needs.

/*
 * The app ships one language, so both formatters pin their locale rather than
 * taking the runtime's. An unpinned Intl formats one way in the adapter-node
 * server process and another in the browser, and a test agrees with whichever
 * machine happened to run it.
 */
const LOCALE = 'en';

/*
 * `numeric: 'always'` rather than 'auto': 'auto' renders ±1 as "yesterday" and
 * "last month", which reads inconsistently in a column beside "3 days ago".
 */
const relativeFormat = new Intl.RelativeTimeFormat(LOCALE, { numeric: 'always' });
const dateFormat = new Intl.DateTimeFormat(LOCALE, { dateStyle: 'medium' });

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
const WEEK = 7 * DAY;
const MONTH = 30 * DAY;
const YEAR = 365 * DAY;

/**
 * The unit to say an elapsed span in, once it has grown past the previous row.
 * Smallest first; scanned in order.
 */
const UNITS: [outgrown: number, unit: Intl.RelativeTimeFormatUnit, size: number][] = [
	[HOUR, 'minute', MINUTE],
	[DAY, 'hour', HOUR],
	[WEEK, 'day', DAY],
	[MONTH, 'week', WEEK],
	[YEAR, 'month', MONTH]
];

/**
 * Whether a timestamp stands for a real moment.
 *
 * Migration 20260905215749_AddProfileTimestamps backfills the rows that predate
 * its two columns with `DateTime.MinValue`, and `DateTimeExtensions.ToJs` turns
 * that into roughly -62135596800000 rather than into a null. So anything at or
 * before the epoch is that backfill, not a date worth printing.
 */
function isKnown(epochMs: number | undefined): epochMs is number {
	return epochMs !== undefined && Number.isFinite(epochMs) && epochMs > 0;
}

/**
 * How long ago a moment was, as a whole phrase: `3 days ago`.
 *
 * Null when the timestamp is absent or is the migration's backfill, so the
 * caller decides what to say instead — the sentence around it lives there.
 *
 * `now` is a parameter rather than a closed-over `Date.now()` so a test can
 * state both ends of the interval instead of mocking the clock.
 */
export function formatTimeAgo(
	epochMs: number | undefined,
	now: number = Date.now()
): string | null {
	if (!isKnown(epochMs)) {
		return null;
	}
	/*
	 * The floor is one minute in both directions, which is why the table above
	 * has no `second` row. Seconds are noise on a card that only refetches when
	 * the page mounts, and without the clamp a client whose clock trails the
	 * backend's would render a last-played time as "in 4 seconds".
	 */
	const elapsed = Math.max(now - epochMs, MINUTE);
	for (const [outgrown, unit, size] of UNITS) {
		if (elapsed < outgrown) {
			// Floor, not round: 59 minutes has to stay "59 minutes ago" rather than
			// rounding into the hour bucket this row was chosen over.
			return relativeFormat.format(-Math.floor(elapsed / size), unit);
		}
	}
	// Nothing to promote a year to, so the table stops short of a row that would
	// exist only to terminate the loop.
	return relativeFormat.format(-Math.floor(elapsed / YEAR), 'year');
}

/**
 * The calendar day a moment fell on: `Sep 5, 2026`. Null on the same terms as
 * {@link formatTimeAgo}.
 *
 * Deliberately the reader's own time zone rather than UTC — a creation date is
 * read as "the day I made this", which is a local fact.
 */
export function formatDate(epochMs: number | undefined): string | null {
	return isKnown(epochMs) ? dateFormat.format(epochMs) : null;
}

/**
 * A contract name as display text: `FoodFarm` becomes `Food farm`.
 *
 * Sentence case, not title case: the design system treats capitalisation as a
 * typographic layer — `oi-label-sm` applies the uppercase a badge wants — and
 * forbids typing it into a string (see styles/openidle/typography.css).
 *
 * An acronym would split letter by letter; no name in types.xml has one.
 */
export function formatWireName(name: string): string {
	return name.replace(/(?!^)([A-Z])/g, (letter) => ` ${letter.toLowerCase()}`);
}
