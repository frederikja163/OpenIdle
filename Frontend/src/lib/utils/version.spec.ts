import { describe, expect, it } from 'vitest';
import { formatUtc, formatVersion } from './version';

// 2026-09-04 23:26:40 UTC, the example the footer's format was specified with.
const COMMIT_TIME = 1_788_564_400_000;
const COMMIT = '1e1c256a0b1c2d3e4f5061728394a5b6c7d8e9f0';

describe('formatUtc', () => {
	it('renders YYYY-MM-DD HH:MM:SS in UTC whatever the local zone is', () => {
		expect(formatUtc(COMMIT_TIME)).toBe('2026-09-04 23:26:40');
	});

	it('zero-pads every field', () => {
		expect(formatUtc(Date.UTC(2026, 0, 2, 3, 4, 5))).toBe('2026-01-02 03:04:05');
	});
});

describe('formatVersion', () => {
	it('joins the commit date and the seven-character sha', () => {
		expect(formatVersion({ commit: COMMIT, commitTime: COMMIT_TIME })).toBe(
			'2026-09-04 23:26:40 1e1c256'
		);
	});

	it('says local for a build outside CI', () => {
		expect(formatVersion({ commit: null, commitTime: null })).toBe('local');
		expect(formatVersion({ commit: '', commitTime: COMMIT_TIME })).toBe('local');
	});

	it('shows a commit without a date on its own rather than inventing one', () => {
		expect(formatVersion({ commit: COMMIT, commitTime: null })).toBe('1e1c256');
	});
});
