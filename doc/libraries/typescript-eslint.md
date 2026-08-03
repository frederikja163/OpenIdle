# typescript-eslint

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 8.65.0 (declared `^8.60.1`)

## 1. Problem

[ESLint](./eslint.md) parses JavaScript. Handed a `.ts` file it fails on the first type annotation — the syntax simply is not JavaScript. Beyond parsing, the more valuable capability is type-aware linting: rules that consult the type checker to catch things neither `tsc` nor a syntax-only linter can, most importantly floating promises. In a client that talks to a game server over async WebSocket calls, an un-awaited promise is a silent dropped action — exactly the failure mode that is hardest to notice and most damaging to a player.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **typescript-eslint 8** (chosen) | 8.65.0, 4 direct deps (its own `parser`, `eslint-plugin`, `utils`, `typescript-estree` sub-packages) | The only TypeScript parser + type-aware rule set for ESLint. `no-floating-promises`, `no-misused-promises`, `await-thenable` require full type information | Very active: 87M weekly downloads, 1498 releases, latest 2026-07-20 | MIT | High: mandatory for linting `.ts` at all; type-aware rules are the real value |
| `@typescript-eslint/parser` alone | same family | Parses TypeScript so ESLint's base rules work, without the plugin's rule set | Same | MIT | Medium: cheaper, but discards type-aware rules — the main reason to have it |
| `tsc --strict` only | already present | Zero new dependencies; catches type errors | n/a | Apache-2.0 | Low: `tsc` does not flag floating promises or unsafe `any` propagation |
| Biome | 2.x | Rust, single dependency, fast; some type-informed rules | Active | MIT/Apache-2.0 | Medium: see [ESLint](./eslint.md) — blocked on Svelte support, and type-aware coverage is narrower |
| Build in-house | n/a | Exactly our rules | Us | n/a | Low: requires a TypeScript AST bridge and type-checker integration |

Why the others lost: parser-only saves almost nothing while giving up the rules that justify the package. `tsc` alone misses the async correctness bugs this project cares most about. Biome remains the interesting long-term option but cannot lint Svelte today.

## 3. Decision & rationale

Adopt. It is required twice over in our setup: `ts.configs.recommended` provides the rule set, and `ts.parser` is configured as the parser for `.svelte` files' script blocks (see the `parserOptions` block in `eslint.config.js`), so both `.ts` files and Svelte components depend on it.

The case rests on type-aware rules rather than parsing. `no-floating-promises` alone is close to justifying the package for this project: a client driving a game over async socket calls will accumulate `await`-less calls, and each one is an action the player took that silently did not happen. Neither TypeScript nor syntax-only linting catches it. The configuration enables `projectService: true` for Svelte files, which is what gives the rules access to full type information.

Two costs are recorded deliberately. First, `projectService` makes linting materially slower, because ESLint now runs a TypeScript program. Second, and more significant: **this package is one of the two reasons the project cannot move to TypeScript 7**. TypeScript 7.0 ships without a stable programmatic compiler API, and typescript-eslint consumes exactly that API. The full analysis and revisit date live in [TypeScript](./typescript.md); the point to note here is that a lint tool is currently constraining the project's language version.

### Pros

- The only way to lint TypeScript with ESLint — no alternative exists.
- Type-aware rules catch a class of async bug that nothing else in the toolchain catches.
- Also serves as the parser for `<script lang="ts">` inside Svelte components, so one package covers both file types.
- Ships as a single meta-package wrapping its four sub-packages, which keeps `package.json` tidy.
- MIT, 87M weekly downloads, very active with a long release history.

### Cons

- Blocks the TypeScript 7 upgrade until its 7.x support lands (expected alongside TypeScript 7.1, roughly October 2026).
- `projectService` linting is slow — it type-checks to lint, roughly doubling the work already done by `tsc`.
- Four sub-packages behind one entry; the real dependency footprint is larger than `package.json` suggests.
- Only 2 npm maintainers for a package installed 87M times weekly.
- Rule set overlaps with `tsc --strict` in places, so some findings are redundant.
- Tightly coupled to TypeScript's internal AST, meaning TypeScript upgrades and typescript-eslint upgrades must be sequenced together.

## 4. Build-vs-buy

Not buildable. The package exists because ESLint and TypeScript have incompatible AST representations; bridging them means maintaining `typescript-estree`, a translation layer that tracks TypeScript's internal AST across every release. Type-aware rules go further and require driving the compiler's type checker from inside a lint rule. This is years of specialised work against a moving, explicitly-unstable internal API — the same instability that is currently blocking the TypeScript 7 upgrade. There is no in-house version of this at any scale, and the fallback of "lint JavaScript only" would mean not linting the project.

## 5. Risk

### Undo risk — low

Two imports in `eslint.config.js` — the recommended config and the parser. No application code touches it. Mechanically trivial to remove; doing so would mean giving up linting for `.ts` and for Svelte script blocks entirely.

### Security risk — low

MIT, actively maintained, no known outstanding CVEs, no native binaries, no install or postinstall scripts. Development-only — never runs in production, ships nothing to the browser.

Two things keep it from being negligible. It expands to four sub-packages, so the real maintainer and publish surface is wider than one entry implies. And like every ESLint plugin it executes with full local privileges during `bun run lint` and continuously in the editor's language server. This is the same class of exposure that produced the incident documented in [eslint-config-prettier](./eslint-config-prettier.md). Exact resolutions in `bun.lock` are the mitigation; treat lockfile updates as reviewable.
