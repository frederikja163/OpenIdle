# eslint-plugin-svelte

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 3.22.0 (declared `^3.19.0`)

## 1. Problem

`.svelte` files are not JavaScript. They contain markup, template directives (`{#each}`, `{#if}`, `bind:`, `on:`), and a `<script>` block, and [ESLint](./eslint.md) cannot parse any of it. Without a Svelte-aware parser, the majority of this client's code is simply not linted. Beyond parsing, Svelte has its own failure modes that generic rules cannot see: unused reactive state, `{@html}` used on untrusted input, missing `key` on keyed each-blocks, and accessibility problems in markup.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **eslint-plugin-svelte 3** (chosen) | 3.22.0, 10 direct deps (incl. `svelte-eslint-parser`, `postcss`, `known-css-properties`) | The only ESLint plugin for Svelte. Bundles `svelte-eslint-parser`; ships Svelte-specific correctness, a11y, and CSS rules; provides `configs.prettier` to defer formatting | Active: 1.5M weekly downloads, 162 releases, latest 2026-07-20 | MIT | High: no alternative exists; without it Svelte files go unlinted |
| No Svelte linting (lint `.ts` only) | 0 bytes | Removes 10 transitive dependencies | n/a | n/a | Low: leaves the bulk of the client unanalysed — components are where the code lives |
| [svelte-check](./svelte-check.md) alone | already present | Already type-checks components and reports a11y and unused-CSS warnings | Active | MIT | **Medium-high**: overlaps more than expected. See below |
| Build in-house | n/a | Exactly our rules | Us | n/a | Low: requires writing a Svelte parser |

Why the others lost: no linting means no linting of the code that matters most. The genuinely interesting challenger is `svelte-check`, addressed directly below.

## 3. Decision & rationale

Adopt — with a caveat recorded honestly rather than buried.

**The overlap question.** [svelte-check](./svelte-check.md) is already installed, already runs over every component, and already reports accessibility warnings and unused CSS selectors. A meaningful share of what `eslint-plugin-svelte` produces is therefore duplicated by a tool we run anyway. That weakens the case for this package considerably, and it is worth being clear that the marginal value is narrower than "we need to lint Svelte files" implies.

What does not overlap is the correctness rule set: `svelte/no-dom-manipulating`, `svelte/require-each-key`, `svelte/no-reactive-reassign`, `svelte/no-at-html-tags`, and the rules around runes usage. These catch Svelte-specific logic errors rather than type errors, and `svelte-check` does not report them. The `{@html}` rule is worth calling out specifically: [Svelte](./svelte.md) escapes interpolated values by default, and `{@html}` is the documented way to bypass that. In a game rendering server-supplied strings — item names, chat, player names — an unflagged `{@html}` is a straightforward XSS hole, and having a linter refuse it by default is a genuine security control rather than a style preference.

That, plus the fact that this is the only ESLint plugin for Svelte in existence, carries the decision. If the overlap with `svelte-check` grows, revisit.

### Pros

- The only option — there is no competing Svelte ESLint plugin.
- Catches Svelte-specific correctness bugs (`each` keys, reactive reassignment, DOM manipulation) that neither `tsc` nor `svelte-check` reports.
- `no-at-html-tags` makes the main XSS vector in Svelte opt-in and visible, which matters for a game rendering server-supplied text.
- Ships `configs.prettier`, which cleanly hands formatting to [Prettier](./prettier.md) without manual rule disabling.
- Bundles `svelte-eslint-parser`, so the parser is not a separate dependency to version-match.

### Cons

- 10 direct dependencies — the heaviest of the four ESLint plugins, pulling `postcss`, `postcss-load-config`, `postcss-safe-parser`, and `known-css-properties` for its CSS rules.
- Meaningful overlap with `svelte-check`, which already covers a11y and unused CSS. We pay for some of the same analysis twice, in both dependencies and lint time.
- Smallest user base in the lint set (1.5M weekly downloads), so bugs surface more slowly than in ESLint core.
- Must track Svelte's syntax; a Svelte major (runes were one) requires a coordinated plugin major.
- Community-maintained rather than first-party to the Svelte core team, unlike `svelte-check`.

## 4. Build-vs-buy

Not buildable. The hard part is `svelte-eslint-parser` — producing an ESTree-compatible AST from a file containing markup, template directives, and script blocks, with source positions accurate enough for autofix, and tracking Svelte syntax across versions. That is a parser project, months of work, permanently chasing upstream.

The realistic in-house alternative is narrower and worth naming: rely on `svelte-check` for a11y and CSS, and enforce the handful of correctness rules we actually care about — chiefly "no `{@html}`" — with a grep in CI. That is genuinely an hour's work and would remove 10 dependencies. It was considered and rejected because a grep cannot distinguish a reviewed, deliberate `{@html}` from a careless one, has no inline-disable mechanism, and covers none of the rune or reactivity rules. Buying wins, but by less than it first appears.

## 5. Risk

### Undo risk — low

Two entries in `eslint.config.js` (`svelte.configs.recommended` and `svelte.configs.prettier`). No application code imports it. Removing it silently stops linting `.svelte` files — cheap mechanically, and `svelte-check` would still cover types and a11y, so the loss is narrower than for most of the lint set.

### Security risk — low

MIT, actively maintained, no known outstanding CVEs, no native binaries, no install or postinstall scripts. Development-only.

Slightly more exposed than the other lint packages on two counts: it has the widest dependency fan-out of the four plugins (10 direct, including the PostCSS chain), and the smallest user base, so a compromised release would be noticed less quickly than one in ESLint core. It runs with full local privileges during `bun run lint` and in the editor — the same class of exposure as the incident in [eslint-config-prettier](./eslint-config-prettier.md). Exact resolutions in `bun.lock` are the mitigation. Note the countervailing point: this package also *reduces* application security risk by flagging `{@html}`, so its net effect is plausibly positive.
