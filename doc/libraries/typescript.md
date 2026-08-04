# TypeScript

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 6.0.3 (declared `^6.0.3`) — deliberately held back from 7.x, see below

## 1. Problem

The client handles the same data the server protects: inventories, currency, levels, skill progress. [C# / .NET](./csharp-dotnet.md) was chosen for the backend explicitly because strong static typing reduces the class of bugs that corrupt player state. Writing the client in plain JavaScript would abandon that protection at the boundary — a mistyped field name or a number/string confusion in a WebSocket payload would fail silently in the browser instead of at compile time. We need static typing on the client for the same reason we chose it on the server.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **TypeScript 6** (chosen) | 6.0.3, 24 MB installed, 0 direct deps | Structural static typing over JavaScript; erases at build time; universal editor support; the stable programmatic compiler API that our tooling depends on | Extremely active: 259M weekly downloads, 3788 releases, Microsoft | Apache-2.0 | High: matches the project's data-safety rationale; supported by every tool we use |
| TypeScript 7 (`tsc`) | 7.0.2, stable since 2026-07-08 | Native Go port of the compiler, shipped in the `typescript` package under the same `tsc` entry point; 8–12× faster full builds. (`tsgo` was the binary name of the `@typescript/native-preview` package this superseded) | Same team, brand new | Apache-2.0 | **Blocked**: no stable programmatic API, so `typescript-eslint` cannot run on it and the Svelte template checker only reaches it through opt-in CLI or experimental routes |
| JSDoc type annotations + `checkJs` | 0 extra bytes | Full type checking with no build step and no syntax that isn't JavaScript | Part of TypeScript itself | Apache-2.0 | Medium: genuinely viable and dependency-free, but far more verbose and awkward for generics |
| Plain JavaScript | 0 bytes | No dependency, no build step, fastest to write | n/a | n/a | Low: discards the guarantee we chose C# for. Inconsistent with the project's stated priorities |
| Flow | 0.2xx | Alternative static type layer | Meta-internal; ecosystem has moved on | MIT | Low: effectively dead outside Meta; no Svelte support |
| Build in-house | n/a | Exactly our needs | Us | n/a | Low: a type system is not a weekend project |

Why the others lost: JSDoc is the only honest challenger — it gives the same checking through the same compiler with zero syntax cost, and for a very small project it would be the more principled choice. It loses on ergonomics: generics, interfaces, and discriminated unions for socket message types are all substantially more painful in JSDoc comments, and the Svelte ecosystem's documentation and examples assume `.ts`. Plain JavaScript loses on principle. Flow is not a live option.

## 3. Decision & rationale

Adopt **TypeScript, pinned to the 6.x major**, and stay there deliberately.

The interesting part of this decision is the version, not the language. TypeScript 7.0 — the native Go compiler, delivering roughly 8–12× faster full builds — went stable on 2026-07-08, and `latest` on npm is now 7.0.2. This project is on 6.0.3 with a `^6.0.3` range that will not cross into 7. **That is correct and intentional, not neglect.**

TypeScript 7.0 ships without a stable programmatic compiler API. Everything that consumes TypeScript as a library rather than as a CLI is therefore blocked: `typescript-eslint`, `ts-morph`, `ts-jest`, and — decisively for us — the template type-checkers behind Svelte, Vue, and Astro. We use two of those directly: [typescript-eslint](./typescript-eslint.md) drives our whole lint configuration, and [svelte-check](./svelte-check.md) is the only thing type-checking our components. Moving to TypeScript 7 today would break the lint configuration outright. `svelte-check` is the softer of the two — 4.7.4 can reach TypeScript 7 through its `--tsgo` CLI route or an experimental native-API flag, both of which this project declines for a CI gate — so it constrains us by choice rather than absolutely. The block is independently confirmed by SvelteKit's own peer dependency range, which currently reads `^5.3.3 || ^6.0.0` — TypeScript 7 is not accepted upstream either.

The programmatic API is expected in TypeScript 7.1, on the team's usual three-to-four-month cadence, putting it around October 2026. **Revisit this document then**, and check three things before moving: that `typescript-eslint` supports 7.x, that `svelte-check`/`svelte2tsx` supports 7.x, and that SvelteKit's peer range accepts it. There is no benefit in moving early — our codebase is far too small for compile speed to be the bottleneck, so the entire upside of TypeScript 7 is currently worth nothing to us while the downside is a broken toolchain.

### Pros

- Catches the class of bug this project most wants to avoid — wrong-shaped data — at compile time, matching the C# rationale.
- Zero direct dependencies and zero runtime cost: types erase entirely, nothing ships to the browser.
- Universal editor support; excellent inference means the annotation burden is modest.
- Apache-2.0, Microsoft-maintained, 259M weekly downloads, no realistic abandonment risk.
- Lets socket message shapes be modelled as discriminated unions, which is the natural fit for the typed event protocol the backend already sends.

### Cons

- 24 MB installed — the single largest package in the frontend tree, roughly 15% of `node_modules`.
- Types describe compile-time intent, not runtime reality. Data arriving from the backend is `any` in truth; without validation at the boundary, TypeScript provides false confidence exactly where player data enters. This is a real gap that needs an explicit answer (hand-written parsers, or a schema validator — the latter would be its own decision document).
- Duplicates the backend's C# DTOs by hand. Two definitions of every message shape, kept in sync by discipline alone. Generating them from the C# side is worth considering later.
- Currently pinned a major behind `latest`, so we forgo an 8–12× compiler speedup until the ecosystem catches up.
- Adds a compile step and its own configuration surface (`tsconfig.json`) to maintain.

## 4. Build-vs-buy

Not buildable. A structural type system with inference is decades of compiler research and Microsoft has spent over a decade on this one. The only in-house-flavoured alternative is JSDoc with `checkJs`, which is not really "building" anything — it uses the same TypeScript compiler with different syntax, so it does not remove the dependency, only the `.ts` extension. Since the package is installed either way, the ergonomic argument decides it and `.ts` wins.

Worth stating clearly: adopting TypeScript does **not** license a validation library, a schema library, or a code-generation tool. The runtime-validation gap noted above is real, and closing it with hand-written parsers for our handful of socket message types is likely an hours-not-weeks job — which under this project's rules means building it, not installing something.

## 5. Risk

### Undo risk — medium

Every `.ts` and `.svelte` file with `lang="ts"` is coupled to it, as are `tsconfig.json`, the ESLint configuration, and [svelte-check](./svelte-check.md). Removing TypeScript means stripping annotations from every file — mechanical, and tooling exists, but it touches everything. Rated `medium` rather than `high` because the direction that matters is reversible cheaply: TypeScript is a superset, so `.ts` files remain valid JavaScript once types are erased, and no runtime behaviour depends on it. Nobody is realistically going to do this anyway.

### Security risk — low

Apache-2.0, Microsoft-maintained, no known outstanding CVEs, zero dependencies, no native binaries, no install scripts. 7 npm maintainers is healthier than most of this project's other dependencies. Compile-time only — nothing reaches the browser. The one genuine security-adjacent concern is not about the package but about what typing implies: static types offer no protection against malformed or hostile data arriving over the socket. All authoritative validation must stay in the C# backend, and the client must not treat a TypeScript interface as evidence that a payload is well-formed.
