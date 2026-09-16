import { describe, expect, it } from 'vitest';
import { formatDate, formatTimeAgo, formatWireName } from './formatUtils';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

// A fixed "now" rather than Date.now(): every case below states both ends of the
// interval, so none of them can drift with the clock or the day they run on.
const NOW = Date.UTC(2026, 8, 5, 12, 0, 0);

describe('formatTimeAgo', () => {
	it('has nothing to say about a timestamp that is not there', () => {
		expect(formatTimeAgo(undefined, NOW)).toBeNull();
	});

	// AddProfileTimestamps backfilled the rows that predate its columns with
	// DateTime.MinValue, which ToJs() turns into a large negative number rather
	// than a null — so this is a real value the backend sends, not a fixture.
	it('has nothing to say about the migration backfill', () => {
		expect(formatTimeAgo(-62135596800000, NOW)).toBeNull();
		expect(formatTimeAgo(0, NOW)).toBeNull();
	});

	it('floors at a minute rather than counting seconds', () => {
		expect(formatTimeAgo(NOW - 5_000, NOW)).toBe('1 minute ago');
	});

	// A client whose clock trails the backend's would otherwise be told a profile
	// was last active in the future.
	it('never reads a clock skewed into the future as time remaining', () => {
		expect(formatTimeAgo(NOW + 5 * MINUTE, NOW)).toBe('1 minute ago');
	});

	it.each([
		[59 * MINUTE, '59 minutes ago'],
		[90 * MINUTE, '1 hour ago'],
		[3 * DAY, '3 days ago'],
		[14 * DAY, '2 weeks ago'],
		[45 * DAY, '1 month ago'],
		[400 * DAY, '1 year ago']
	])('says %i ms ago as "%s"', (elapsed, expected) => {
		expect(formatTimeAgo(NOW - elapsed, NOW)).toBe(expected);
	});
});

describe('formatDate', () => {
	it('has nothing to say about the migration backfill', () => {
		expect(formatDate(-62135596800000)).toBeNull();
		expect(formatDate(undefined)).toBeNull();
	});

	// The day itself is deliberately the reader's own, so asserting one would fail
	// east of UTC+12 for an instant near midnight. The shape is what can regress.
	it('gives a medium-format calendar day', () => {
		expect(formatDate(NOW)).toMatch(/^[A-Z][a-z]{2} \d{1,2}, \d{4}$/);
	});
});

describe('formatWireName', () => {
	it.each([
		['Stone', 'Stone'],
		['FoodFarm', 'Food farm'],
		['CraftBalsaHandle', 'Craft balsa handle']
	])('says %s as "%s"', (wire, expected) => {
		expect(formatWireName(wire)).toBe(expected);
	});
});
