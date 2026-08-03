# svelte-check

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 4.7.4 (declared `^4.6.0`)

## 1. Problem

[TypeScript](./typescript.md)'s `tsc` cannot type-check a `.svelte` file. It sees an unfamiliar extension containing markup, a `<script>` block, and Svelte-specific template syntax, and skips it. That means the majority of a Svelte client's code — every component's props, event handlers, and template expressions — would go completely unchecked, which defeats the point of choosing TypeScript at all. We need something that type-checks components as part of `bun run check` and in CI.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **svelte-check 4** (chosen) | 4.7.4, 6 direct deps (`chokidar`, `fdir`, `sade`, `picocolors`, `@jridgewell/trace-mapping`, `@sveltejs/load-config`) | Uses `svelte2tsx` to transform components into checkable TypeScript, then runs the TS language service. Also surfaces unused-CSS and a11y warnings. `--watch` mode | Very active: 2.3M weekly downloads, latest 2026-07-27, maintained by the Svelte team | MIT | High: the only real way to type-check components; already wired into `bun run check` |
| Editor-only checking (Svelte for VS Code) | n/a | Same diagnostics, zero added dependency — the extension bundles the language tools itself | Same team | MIT | Low: cannot run in CI or as a pre-commit gate. Errors ship if the author's editor was closed |
| `tsc` alone on `.ts` files only | already present | No new dependency | n/a | Apache-2.0 | Low: leaves all component code unchecked — the large majority of the client |
| No type checking of components | n/a | Nothing to install | n/a | n/a | Low: contradicts the data-safety rationale that drove the TypeScript and C# choices |
| Build in-house | n/a | Exactly our needs | Us | n/a | Low: requires reimplementing `svelte2tsx`. See build-vs-buy |

Why the others lost: the editor extension gives identical diagnostics but only to whoever has the editor open, which makes it a developer convenience rather than an enforceable check. Everything else amounts to accepting that components are unchecked, which is not consistent with a project that chose static typing on both ends specifically to protect player data.

## 3. Decision & rationale

Adopt. This is the enforcement half of the [TypeScript](./typescript.md) decision — without it, TypeScript covers `vite.config.ts` and a handful of `lib` files while the actual UI goes unchecked. It is already wired into the `check` and `check:watch` scripts and should be treated as a required CI gate rather than an optional local command.

Worth noting that `svelte-check` and the Svelte VS Code extension share the same underlying `svelte2tsx` machinery, so the CLI and the editor agree on diagnostics. That consistency is worth something on its own: a check that disagrees with the editor gets ignored.

One coupling to record explicitly: `svelte-check` depends on `svelte2tsx`, which uses TypeScript's programmatic compiler API. That API is not stable in TypeScript 7, which is the direct reason this project stays on TypeScript 6 — see [TypeScript](./typescript.md) for the full analysis and the revisit date.

### Pros

- The only practical way to type-check `.svelte` files in CI.
- Same diagnostics engine as the official editor extension, so local and CI results match.
- Catches more than types: unused CSS selectors and accessibility warnings come free, and a11y warnings are genuinely useful for a UI nobody else will review.
- 6 small direct dependencies; no native binaries, no install scripts.
- First-party, MIT, 2.3M weekly downloads, released roughly weekly.

### Cons

- Noticeably slow on large projects — it is the TypeScript language service running over generated code. Not a concern at current size; will become one.
- Diagnostics point at positions in generated TSX, and while sourcemaps usually map them back cleanly, confusing off-by-a-bit errors do occur in edge cases.
- Adds a mandatory `svelte-kit sync` step before it can run (visible in the `check` script), so it cannot be run against a clean checkout without a prior generate step.
- Its dependency on the TypeScript compiler API is what pins the whole project to TypeScript 6. That is a real constraint imposed by a checking tool, not by the language choice itself.

## 4. Build-vs-buy

Not a credible build. The hard part is `svelte2tsx` — transforming a component's markup, template expressions, slots, snippets, bindings, and generics into TypeScript that type-checks *and* maps errors back to original source positions. That is a compiler, and it has to track every Svelte syntax change. Months of work, permanently behind upstream. The realistic in-house alternative is "don't check components", which was evaluated above and rejected. Buying wins without argument.

## 5. Risk

### Undo risk — low

Invoked only from two `package.json` scripts. No source file imports it and no configuration outside `tsconfig.json` (which TypeScript needs anyway) exists for it. Removing it deletes two script lines and silently loses component type coverage — cheap to undo mechanically, though the coverage loss is the real cost.

### Security risk — low

Development-only; never runs in production and ships nothing to the browser. MIT, first-party, actively maintained, no known CVEs. Dependencies are small and widely used. No native binaries, no install or postinstall scripts. Standard build-toolchain exposure only — it runs with local privileges during `bun install` and `bun run check`, the same as everything else in `devDependencies`.
