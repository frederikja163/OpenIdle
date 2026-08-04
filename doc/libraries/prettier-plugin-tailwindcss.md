# prettier-plugin-tailwindcss

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 0.8.1 (declared `^0.8.0`) — note: pre-1.0

## 1. Problem

Tailwind encourages long `class` attributes built from many utility classes. Written by hand they end up in arbitrary order, so two elements with identical styling can look completely different in a diff, and reviewing a class-list change means reading it character by character. Sorting classes into a canonical order makes them comparable. This plugin does that automatically as part of formatting.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **prettier-plugin-tailwindcss** (chosen) | 0.8.1, 0 direct deps | Sorts Tailwind classes into Tailwind's own canonical order during formatting; reads the project stylesheet to understand custom utilities | Very active: 8.7M weekly downloads, 262 releases, latest 2026-07-15, maintained by Tailwind Labs | MIT | High: zero-cost companion to [Tailwind](./tailwindcss.md); mitigates the verbose-markup cost that decision accepted |
| No sorting (write classes by hand) | 0 bytes | One fewer package and one fewer plugin-ordering constraint | n/a | n/a | Medium: workable; class lists become inconsistent and diffs noisier |
| Drop [Tailwind](./tailwindcss.md) entirely | 0 bytes | Removes this plugin, `tailwindcss`, and `@tailwindcss/vite` — three packages | n/a | n/a | Low: Tailwind was adopted on an explicit argument; that decision is made elsewhere and is not reopened here |
| ESLint-based class sorting | varies | Sorting as a lint rule instead of a format step | Community plugins, less maintained | MIT | Low: slower, and conflicts with Prettier owning formatting |
| Build in-house | n/a | Our own sort order | Us | n/a | Low: requires resolving Tailwind's class ordering. See build-vs-buy |

Why the others lost: dropping Tailwind is settled elsewhere and not reopened here. The only live alternative is "keep Tailwind and don't sort" — workable, but it gives up canonical ordering for no saving beyond one zero-dependency package.

## 3. Decision & rationale

Adopt, following [Tailwind CSS](./tailwindcss.md). This package has no standing of its own — it is the natural companion to that decision and is removed with it if that is ever reversed.

It costs almost nothing — zero dependencies, maintained by Tailwind Labs, and already configured correctly in `prettier.config.js` (registered after `prettier-plugin-svelte`, with `tailwindStylesheet` pointing at `src/routes/layout.css` so project-specific utilities sort correctly).

It also reinforces the specific reasons Tailwind was adopted. Consistency was the decisive argument there, and canonical class ordering extends that from *which* values are used to *how* they read: two elements with identical styling produce identical class strings, so a diff shows what actually changed rather than a reshuffle. And since the main cost accepted in the Tailwind decision was verbose markup, a tool that keeps long class lists in a predictable order is a partial mitigation of that cost rather than an unrelated nicety.

One further flag: the package is at **0.8.1, still pre-1.0**, after 262 releases and more than four years of development. In semver terms the authors have not committed to API stability. In practice it is heavily used and Tailwind-Labs-maintained, so this is a formality rather than a live concern — but it is worth recording, since a pre-1.0 version number is normally something this project would treat as a caution.

### Pros

- Zero direct dependencies.
- Maintained by Tailwind Labs, so class ordering matches Tailwind's own canonical order and tracks new utilities automatically.
- Removes an entire category of pointless diff noise and eliminates "what order do classes go in" as a question.
- Reads the project stylesheet, so custom `@utility` definitions sort correctly rather than being dumped at the end.
- 8.7M weekly downloads; frequent releases.

### Cons

- No independent standing: if Tailwind is ever dropped, this is dead weight and goes with it.
- Pre-1.0 version number after four years, so no formal API stability commitment.
- Introduces the plugin-ordering constraint in `prettier.config.js` (must come after `prettier-plugin-svelte`), which fails silently when wrong.
- Adds work to every format run for a purely cosmetic benefit.

## 4. Build-vs-buy

Neither, really — the honest framing is buy-or-skip. Reimplementing Tailwind's class order in-house would mean encoding the utility ordering and keeping it synchronised with Tailwind releases, which is pointless work when the upstream team publishes exactly that as a zero-dependency package.

Skipping is the genuine alternative: keep Tailwind, write class lists by hand in whatever order they occur, and accept noisier diffs. That costs nothing and removes a package. It is a reasonable choice for a solo project, and would be the correct one if the plugin had any real weight — but at zero dependencies, the trade favours keeping it *provided Tailwind itself is justified*. Which returns to the open question.

## 5. Risk

### Undo risk — low

One entry in the `plugins` array and one `tailwindStylesheet` line in `prettier.config.js`. Removing it changes no source code — already-sorted classes stay sorted. The cheapest possible removal.

### Security risk — low

Development-only; never runs in production, ships nothing to the browser. Zero direct dependencies, no native binaries, no install or postinstall scripts. MIT, maintained by Tailwind Labs, no known CVEs.

Standard Prettier-plugin exposure applies: it executes with local privileges during `bun run format` and on editor save, the same category as the packages hit in the July 2025 npm compromise described in [eslint-config-prettier](./eslint-config-prettier.md). With no dependencies and no install hooks, its own surface is minimal, and exact pinning in `bun.lock` covers the realistic case.
