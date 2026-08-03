# ESLint

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 10.8.0 (declared `^10.4.1`)

## 1. Problem

[TypeScript](./typescript.md) checks that types line up. It does not catch unreachable code, unused variables, floating promises, accidental `==`, unhandled `await` in loops, or Svelte-specific mistakes like a reactive statement that never re-runs. On a solo project there is no code reviewer, so the only thing standing between a careless mistake and `main` is automated analysis. A linter is the second pair of eyes.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **ESLint 10** (chosen) | 10.8.0, 3.8 MB, 30 direct deps | The de facto standard; flat config; the only linter with a mature Svelte plugin and full type-aware TypeScript rules | Extremely active: 155M weekly downloads, 426 releases, OpenJS Foundation | MIT | High: the only option with first-class Svelte support |
| Biome | 2.x | Rust-based lint + format in one tool, ~25× faster, single dependency, no plugin sprawl | Active and growing fast | MIT/Apache-2.0 | Medium: would replace ESLint *and* [Prettier](./prettier.md) and delete 8 packages — genuinely attractive. Loses on Svelte support |
| Oxlint | 1.x | Rust-based, extremely fast, ESLint-rule-compatible subset | Active (VoidZero, same org as Vite) | MIT | Medium: fast and lean, but type-aware rules and Svelte support are still incomplete |
| `tsc` strict mode only | already present | Zero new dependencies; catches a real subset of the same bugs | n/a | Apache-2.0 | Medium: the honest minimalist answer. Misses everything non-type-related |
| No linter | 0 bytes | Nothing to install or configure | n/a | n/a | Low: on a solo project this means nothing reviews the code at all |
| Build in-house | n/a | Exactly our rules | Us | n/a | Low: an AST analysis framework is not an hours-not-weeks item |

Why the others lost: Biome is the strongest challenger and deserves a straight answer. It would collapse ESLint, [@eslint/js](./eslint-js.md), [typescript-eslint](./typescript-eslint.md), [eslint-plugin-svelte](./eslint-plugin-svelte.md), [eslint-config-prettier](./eslint-config-prettier.md), [globals](./globals.md), [Prettier](./prettier.md), and [prettier-plugin-svelte](./prettier-plugin-svelte.md) — eight of our twenty devDependencies — into one. For a project whose stated principle is minimal dependencies, that is a serious argument. It loses today on one blocking fact: Svelte support is not there. A linter that cannot read `.svelte` files cannot lint the majority of this codebase, and no amount of dependency reduction compensates for that. Oxlint has the same gap plus incomplete type-aware rules.

## 3. Decision & rationale

Adopt **ESLint 10**, with the Biome question left explicitly open.

ESLint is chosen because it is the only linter that can actually lint this project. The Svelte ecosystem's tooling — `eslint-plugin-svelte`, `svelte-eslint-parser` — is built for ESLint and has no equivalent elsewhere. Combined with `typescript-eslint` for type-aware rules, it covers both halves of the codebase, which nothing else currently does.

The cost should be stated honestly rather than glossed over: ESLint alone has 30 direct dependencies, and the working lint setup requires six packages in total ([@eslint/js](./eslint-js.md), [typescript-eslint](./typescript-eslint.md), [eslint-plugin-svelte](./eslint-plugin-svelte.md), [eslint-config-prettier](./eslint-config-prettier.md), [globals](./globals.md), and ESLint itself). That is nearly a third of the frontend dependency count spent on linting. It is justified only because there is no code reviewer on this project, which makes automated analysis do a job a human would otherwise do.

**Revisit when Biome ships stable Svelte support.** At that point the trade becomes eight packages down to one, and the case for staying on ESLint would need to be re-made rather than assumed.

### Pros

- The only linter with mature Svelte support, which is decisive here.
- Flat config (`eslint.config.js`) is plain ESM — readable, debuggable, no bespoke config-resolution magic.
- Enormous rule ecosystem; type-aware rules via typescript-eslint catch genuine bugs like floating promises.
- `includeIgnoreFile` reuses `.gitignore` directly, so ignore rules live in one place rather than two.
- MIT, OpenJS Foundation governance — no single-vendor dependency.
- 155M weekly downloads; effectively no abandonment risk.

### Cons

- 30 direct dependencies for the core package, and six packages for a working setup — by far the heaviest tooling area in the project.
- Slow relative to Rust-based alternatives, and type-aware linting via `projectService` compounds it. Fine now; noticeable later.
- Only 2 npm maintainers on a package installed 155M times weekly.
- Flat config was a disruptive migration and plugin compatibility still varies; configuration debugging is a recurring cost.
- Overlaps with what [TypeScript](./typescript.md) already catches — a portion of the rule surface is redundant with a strict `tsc`.
- Rule configuration is unbounded: without discipline, linting becomes a bikeshedding surface rather than a bug-catching one.

## 4. Build-vs-buy

Not a real build. A linter needs a parser producing a stable AST for TypeScript *and* Svelte, a rule engine with scope analysis, autofix with conflict resolution, and a config resolution system. Reaching parity with even the handful of rules we rely on would be weeks, and the Svelte parser alone is a project in its own right.

The genuinely minimal in-house alternative is not "write a linter" but "rely on `tsc --strict` and skip linting entirely" — zero dependencies, and it does catch a real subset of what ESLint catches. That option was weighed seriously and rejected: on a project with no reviewer, the non-type bugs ESLint catches (floating promises, unused code, Svelte reactivity mistakes, a11y issues) are exactly the ones that otherwise reach production unexamined.

## 5. Risk

### Undo risk — low

Confined to `eslint.config.js` and the `lint` script. No source file imports ESLint, and removing it changes no runtime behaviour — it only stops reporting problems. The five companion packages come out with it. Mechanically trivial; the cost is losing the analysis, not disentangling code.

### Security risk — low

MIT, OpenJS Foundation governance, no known outstanding CVEs, no native binaries, no install or postinstall scripts anywhere in its tree. Development-only; nothing reaches production or the browser.

The real exposure here is structural and worth naming: the lint setup pulls the largest number of separate packages and maintainers of any area in this project, and ESLint plugins are ordinary npm packages executed with full local privileges during `bun run lint` and in the editor. This project has already been directly exposed to that pattern once — see [eslint-config-prettier](./eslint-config-prettier.md), where a phished maintainer published Windows RCE malware into a package we depend on. The mitigations are the ones already in place: exact resolutions pinned in `bun.lock`, no automatic updates, and treating any lockfile change as a reviewable event.
