# @eslint/js

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 10.0.1 (declared `^10.0.1`)

## 1. Problem

[ESLint](./eslint.md) ships with rules but no opinion about which of them to switch on. A linter with every rule off reports nothing. Someone has to decide the baseline set — the rules that catch outright mistakes rather than express style preferences. Either we curate that list ourselves and maintain it as ESLint's rule set evolves, or we use the one the ESLint team publishes.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@eslint/js** (chosen) | 10.0.1, 0 direct deps | The ESLint team's own baseline configs: `js.configs.recommended` (probable-bug rules only) and `js.configs.all`. Versioned and released alongside ESLint core | Very active: 136M weekly downloads, latest 2026-02-06, same team as ESLint | MIT | High: curated by the people who wrote the rules; zero dependencies |
| Build in-house (hand-curated rule list) | ~40 lines in `eslint.config.js` | Total control; nothing installed; every enabled rule consciously chosen | Us | n/a | Medium: the real challenger. See build-vs-buy |
| `eslint-config-airbnb` / `eslint-config-standard` | various | Comprehensive opinionated presets covering style as well as correctness | Community-maintained, variable | MIT | Low: heavily style-opinionated, which collides with [Prettier](./prettier.md), and each pulls further plugin dependencies |
| No baseline (rules off by default) | 0 bytes | Nothing installed | n/a | n/a | Low: a linter that reports nothing is not a linter |

Why the others lost: Airbnb-style presets bring style rules we do not want (Prettier owns formatting) plus their own plugin dependencies, which is the opposite of what this project optimises for. A hand-curated list is a legitimate alternative and is addressed below.

## 3. Decision & rationale

Adopt. `js.configs.recommended` is used in `eslint.config.js` as the base layer under `typescript-eslint` and `eslint-plugin-svelte`.

The distinguishing quality is that ESLint's `recommended` set is deliberately narrow: it enables rules that flag *probable bugs*, not stylistic preferences. That matters here because [Prettier](./prettier.md) owns formatting and [eslint-config-prettier](./eslint-config-prettier.md) exists to switch off anything stylistic — a preset heavy on style would fight that arrangement. The ESLint team's baseline is already scoped the way we want.

The package is also as cheap as a dependency gets: zero direct dependencies, and it is effectively part of ESLint itself, split into a separate package purely so configs can be versioned independently. Installing ESLint without it and then hand-writing the equivalent baseline would add work without removing a maintainer or a supply-chain hop of any consequence.

### Pros

- Zero direct dependencies — the leanest package in the entire frontend set.
- Curated by the ESLint team, so new rules are triaged into `recommended` by the people who understand their false-positive rates.
- Deliberately limited to probable-bug rules; no style opinions to conflict with Prettier.
- Versioned in lockstep with ESLint core, so upgrades are coherent.
- MIT, 136M weekly downloads.

### Cons

- An additional package entry for what is conceptually part of ESLint — mild dependency-count noise.
- `recommended` changes between majors, so an ESLint upgrade can silently enable new rules and produce new errors on unchanged code.
- Its content is opaque at a glance: we do not see which rules are on without consulting the docs, so the effective configuration is less legible than an explicit list would be.

## 4. Build-vs-buy

The honest alternative — writing our own baseline rule list — is genuinely an hours-not-weeks job, which by this project's rules is normally a reason to build. Enumerating the roughly 40 rules in `recommended` into `eslint.config.js` is an afternoon, and the result would be *more* legible than an opaque preset.

Buying still wins, on maintenance rather than effort. A hand-written list is a snapshot: it never learns about new rules added in later ESLint versions, and it never drops rules the team retires because they proved noisy. Keeping it current means re-reading ESLint release notes at every upgrade — small, recurring, and exactly the kind of chore a solo maintainer silently stops doing. Since the package costs zero dependencies and comes from the same team as the linter it configures, there is nothing to gain by owning it.

Worth noting the inverse conclusion reached for [eslint-config-prettier](./eslint-config-prettier.md), where a similar "it's just a list of rules" argument comes out closer.

## 5. Risk

### Undo risk — low

One import and one entry in `eslint.config.js`. Replacing it with an explicit rule list is an afternoon's work at any time, and nothing else in the project references it.

### Security risk — low

Zero dependencies, no executable logic beyond exporting config objects, no native binaries, no install scripts. Development-only. MIT, ESLint team, no known CVEs. The generic npm supply-chain exposure applies — it is code the linter loads with local privileges — but with no dependencies and no install hooks its attack surface is about as small as an npm package's can be. Covered by lockfile pinning in `bun.lock`.
