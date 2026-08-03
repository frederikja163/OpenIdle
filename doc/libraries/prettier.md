# Prettier

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 3.9.6 (declared `^3.8.3`)

## 1. Problem

Formatting decisions — indentation, quote style, line breaks, trailing commas — are individually trivial and collectively a constant low-grade drain. On a solo project there is no team to argue with, but the cost does not vanish: it reappears as inconsistent files, noisy diffs where a reformat obscures a real change, and time spent manually aligning things. The backend already gets this for free (`dotnet format` and .editorconfig conventions are established in C#); the frontend has no such default.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Prettier 3** (chosen) | 3.9.6, 9.7 MB, 0 direct deps | The de facto standard; deliberately few options; plugin system covers Svelte and Tailwind class sorting; formats TS, JS, CSS, HTML, JSON, Markdown, YAML from one tool | Very active: 128M weekly downloads, latest 2026-07-21, 11 npm maintainers | MIT | High: only formatter with a mature Svelte plugin |
| Biome | 2.x | Rust; formats *and* lints in one tool; ~25× faster; would remove 8 packages total | Active, growing | MIT/Apache-2.0 | Medium: strong candidate, blocked on Svelte support — see [ESLint](./eslint.md) |
| `dprint` | 0.4x | Rust, fast, pluggable | Active but small community | MIT | Low: no mature Svelte plugin; much smaller ecosystem |
| Editor formatting only (VS Code built-in) | 0 bytes | No dependency | n/a | n/a | Low: cannot be enforced in CI; degrades to per-machine settings |
| No formatter | 0 bytes | Nothing to install | n/a | n/a | Medium: honest option for a solo project. Loses on diff noise and the `.svelte` case |
| Build in-house | n/a | Exactly our style | Us | n/a | Low: a pretty-printer for four languages is not an hours-not-weeks job |

Why the others lost: Biome is the real competitor and loses on the same blocking issue as in the ESLint decision — it cannot format `.svelte` files, which are the bulk of this codebase. Editor-only formatting cannot be enforced. "No formatter" is defensible for a solo developer but gives up consistent diffs, which matter more in an open-source project that may take outside contributions.

## 3. Decision & rationale

Adopt **Prettier 3**. The configuration in `prettier.config.js` is deliberate rather than default: tabs, single quotes, no trailing commas, 100-column width. Tabs in particular are an accessibility-positive default (readers can set their own indent width), and the settings match the `sv` scaffold conventions the Svelte ecosystem uses.

The decisive factor, as with the linter, is Svelte support. `prettier-plugin-svelte` is the only mature formatter for `.svelte` files, and it is a Prettier plugin — choosing a different formatter means not formatting components. For an open-source project intending to accept contributions, a mechanical formatter also removes style from code review entirely, which is worth more than its cost.

The cost worth naming: Prettier is 9.7 MB installed, second only to [TypeScript](./typescript.md), and it exists alongside [ESLint](./eslint.md) with an entire package ([eslint-config-prettier](./eslint-config-prettier.md)) required just to stop the two from fighting. Three packages to format and lint, where Biome would eventually be one. That is the trade being accepted, and it should be re-examined when Biome ships Svelte support.

### Pros

- Zero direct dependencies, despite its size — nothing beneath it to audit.
- Deliberately few configuration options, so formatting never becomes a discussion.
- One tool covers TypeScript, JavaScript, CSS, HTML, JSON, Markdown, and YAML.
- The only formatter with mature Svelte support, via a plugin.
- 11 npm maintainers — the healthiest maintainer count of any package in this set, and a meaningfully lower single-account compromise risk than the two-maintainer packages here.
- MIT, 128M weekly downloads, no realistic abandonment risk.

### Cons

- 9.7 MB installed for a formatter.
- Requires [eslint-config-prettier](./eslint-config-prettier.md) purely to prevent conflicts with ESLint — a dependency that exists only because we run two overlapping tools.
- Slow compared with Rust-based formatters; noticeable on format-on-save in large files.
- Opinionated output is occasionally worse than hand-formatting, particularly for long chained expressions and dense JSX-like markup. Accepting it means accepting the bad cases too.
- Plugin ordering matters and is under-documented: `prettier-plugin-svelte` must precede `prettier-plugin-tailwindcss`, which is easy to break and produces confusing results.

## 4. Build-vs-buy

Not buildable. A formatter must parse four or more languages into full-fidelity syntax trees (preserving comments and their attachment points, which is the genuinely hard part), then re-print with a line-breaking algorithm that respects a width budget. Prettier's Svelte plugin has to do this for markup, directives, and script blocks together. This is years of work, and the failure mode of getting it wrong is a tool that silently corrupts source files.

The real in-house alternative is "no formatter, enforce nothing" — genuinely zero-cost and viable for a single developer. It was rejected because the project is open source and expects contributors, and because `.svelte` files with mixed markup and logic are exactly where inconsistent formatting compounds fastest.

## 5. Risk

### Undo risk — low

Confined to `prettier.config.js`, the `lint` and `format` scripts, and `.prettierignore`. No source file imports Prettier. Removing it leaves the code as-is — formatting already applied does not revert. The two Prettier plugins and `eslint-config-prettier` would come out with it.

### Security risk — low

MIT, no known outstanding CVEs, zero direct dependencies, no native binaries, no install or postinstall scripts. Development-only; nothing reaches the browser. 11 npm maintainers is the strongest governance signal in this dependency set — meaningfully more resilient to the single-phished-account attack that produced CVE-2025-54313.

One connected note: the July 2025 attack that compromised [eslint-config-prettier](./eslint-config-prettier.md) also hit `eslint-plugin-prettier` (a package we deliberately do not use). Prettier core itself was not affected, but the incident is a reminder that the Prettier plugin ecosystem is a target. Our two plugins — [prettier-plugin-svelte](./prettier-plugin-svelte.md) and [prettier-plugin-tailwindcss](./prettier-plugin-tailwindcss.md) — carry that generic exposure, mitigated by exact pinning in `bun.lock`.
