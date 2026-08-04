# @sveltejs/vite-plugin-svelte

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 7.2.0 (declared `^7.1.2`)

## 1. Problem

[Vite](./vite.md) does not know what a `.svelte` file is. Something has to intercept those files during dev and build, hand them to the Svelte compiler, and wire the results into Vite's module graph and hot-module-reload machinery. Without it the build simply fails on the first component import.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@sveltejs/vite-plugin-svelte 7** (chosen) | 7.2.0, 4 direct deps (`deepmerge`, `magic-string`, `obug`, `vitefu`) | The official Svelte/Vite integration; declared peer dependency of `@sveltejs/kit`; handles HMR, scoped CSS extraction, and preprocessing | Very active: 3.0M weekly downloads, latest 2026-07-07, maintained by the Svelte team | MIT | High: it is the only supported option |
| Build in-house (own Vite plugin calling `svelte/compiler`) | n/a | Full control; the naive version is genuinely short | Us | n/a | Low: correctness is in the details we would get wrong |
| No plugin / different bundler | n/a | Avoids the dependency by abandoning Vite | n/a | n/a | Low: replaces one dependency with a much larger problem |

Why the others lost: there is no second-source alternative. [SvelteKit](./sveltekit.md) declares this package as a peer dependency (`^3.0.0 || ^4 || ^5 || ^6 || ^7`), so it must be installed regardless of what we would prefer.

## 3. Decision & rationale

Adopt. This is not really an independent decision — it is the mechanical consequence of choosing Svelte and Vite together, and it is a required peer dependency of SvelteKit. It is listed explicitly in `devDependencies` rather than left implicit because it is a peer dependency, not a transitive one: package managers do not install it automatically, and SvelteKit's peer range spans five majors, so the version we get should be a deliberate choice rather than whatever resolution happens to pick.

The only genuine judgement call recorded here is that we accept the plugin's version range coupling: `@sveltejs/kit` accepts plugin v3 through v7, so a plugin major bump is a change we can make independently of Kit — worth knowing when either one needs upgrading.

### Pros

- Official and first-party; version-tracks Svelte and SvelteKit releases.
- Only 4 direct dependencies, all small and build-time only.
- Handles the parts of the integration that are easy to get subtly wrong: HMR state preservation, scoped CSS extraction into the module graph, sourcemap chaining through preprocessing.
- 3.0M weekly downloads means integration bugs surface and get fixed quickly.

### Cons

- One more package in `devDependencies` that exists purely as glue.
- Coupled to Vite's plugin API, so a Vite major can require a plugin major in lockstep — a small but real upgrade-ordering constraint.
- Its configuration surface overlaps confusingly with SvelteKit's: our `vite.config.ts` passes `compilerOptions.runes` through `sveltekit()`, not through the plugin directly, which is easy to misremember when debugging.

## 4. Build-vs-buy

A minimal Vite plugin that calls `svelte/compiler` on `.svelte` files is perhaps 50 lines and looks like an easy afternoon. That estimate is wrong once you include what actually matters: hot-module-reload that preserves component state, correct sourcemap chaining through preprocessors, scoped-CSS extraction as separate virtual modules, and dependency-graph invalidation so editing a child re-renders its parents. Realistically a week to reach something usable and an ongoing tax every Vite and Svelte major. Buying wins decisively — and in any case SvelteKit requires this exact package as a peer, so building would mean installing it anyway and using our own instead. Not a real choice.

## 5. Risk

### Undo risk — low

Confined to one line in `vite.config.ts` (invoked indirectly via `sveltekit()`). Nothing in application code imports it. It disappears the moment Svelte or Vite is dropped, and has no independent hold on the codebase.

### Security risk — low

Build-time only; nothing reaches the browser. MIT, first-party, actively maintained, no known CVEs, four small well-known dependencies, no native binaries and no install scripts. Its blast radius is the same as the rest of the build toolchain: it runs with full local privileges during `bun install` and `vite build`, so it inherits the general npm supply-chain exposure documented in [eslint-config-prettier](./eslint-config-prettier.md), but carries no package-specific concern of its own.
