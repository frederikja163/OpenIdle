# SvelteKit (@sveltejs/kit)

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 2.70.2 (declared `^2.63.0`)

## 1. Problem

[Svelte](./svelte.md) renders components but says nothing about how a user gets from one screen to another, how the app is built for production, or how it is served. A game client needs at minimum: URL-based routing (character screen, inventory, skills, settings), code splitting so the initial load is not the whole game, a dev server with hot reload, and a production build that can be dropped onto static hosting or served by the backend. We need the application shell around the component framework.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **SvelteKit 2** (chosen) | 2.70.2, 1.7 MB, 12 direct deps | Filesystem routing, automatic code splitting, SSR/SSG/SPA all configurable, adapters for any deploy target, first-party to Svelte so upgrades stay in lockstep | Very active: 2.4M weekly downloads, 977 releases, latest 2026-07-29, same team as Svelte | MIT | High for routing + build; its server-side half largely duplicates our C# backend (see pushback below) |
| Plain Svelte + [Vite](./vite.md) + a router | vite only, + ~50 KB router | Strictly less machinery. No server layer at all, which honestly matches our architecture — the backend is C# | Router libraries (e.g. `svelte-routing`) are small and less actively maintained | MIT | Medium-high: the leanest option that still works, and the strongest challenger. Loses on code splitting, prerendering, and having to pick + own a router |
| Plain Svelte + Vite + hand-rolled router | vite only | Zero routing dependency. History API + a `$state` route match is genuinely ~100 lines | Us | n/a | Medium: real candidate, see build-vs-buy |
| Astro | 5.x | Excellent for content sites, islands architecture | Very active | MIT | Low: optimised for content-heavy static pages, not a single stateful long-lived app screen |
| Build in-house (full app shell) | n/a | Exactly our needs | Us | n/a | Low: routing is buildable, but build config, code splitting, and prerendering are not hours-of-work items |

Why the others lost: Astro is the wrong shape for a persistent single-screen game. The two "plain Vite" options are genuinely competitive and are addressed head-on below rather than dismissed.

## 3. Decision & rationale

Adopt **SvelteKit 2**, with an explicit architectural constraint recorded here.

**The pushback, stated plainly:** SvelteKit is a full-stack meta-framework. Roughly half of what it offers — server routes (`+server.ts`), form actions, `load` functions running on the server, and remote functions — is application-server functionality that this project already has, written in C#, documented in [ASP.NET Core](./aspnet-core.md). Adopting SvelteKit without a decision here means quietly acquiring a second backend in a second language, with a second deployment, for a game whose authoritative state must live in exactly one place. For an idle MMORPG that is not a stylistic concern: any game logic that leaks into the Node layer is logic a player can reach outside the C# server's validation.

The decision therefore is: **adopt SvelteKit for routing, code splitting, and the build pipeline; treat its server-side capabilities as out of bounds.** The client talks to the C# backend over HTTP and WebSockets and to nothing else. Concretely, this means the app should move to a static/SPA build (`@sveltejs/adapter-static` with `ssr = false`) rather than remain on the SSR-capable default — see [@sveltejs/adapter-auto](./sveltejs-adapter-auto.md), which is still an unresolved placeholder.

With that constraint, SvelteKit earns its place: filesystem routing, automatic per-route code splitting, `svelte-kit sync` generating route types, and a maintained build pipeline are all things we would otherwise write and own. It is also first-party to Svelte, so the framework and its shell version together — a real maintenance saving over pairing Svelte with a third-party router that may lag a Svelte major.

### Pros

- Filesystem routing and automatic code splitting for free — both would otherwise be hand-built and maintained.
- First-party to Svelte: no version-skew risk between framework and app shell.
- Generated types for routes and params, which keeps the client honest about its own URLs.
- Configurable all the way down to a pure static SPA, so the "second backend" risk is avoidable by configuration rather than by replacing the framework.
- 12 direct dependencies is modest for what it does, and all are small, well-known packages (`cookie`, `devalue`, `sirv`, `acorn`, `magic-string`).
- 2.4M weekly downloads, releases roughly weekly, MIT.

### Cons

- Ships a large server-side surface we have decided not to use. Unused capability is not free: it is documentation to ignore, examples that mislead, and a standing temptation to put game logic in the wrong process.
- Its own release cadence is fast (977 releases; remote functions changed materially across 2.56–2.61 during 2026). Minor versions move quickly, so `^2.63.0` will drift.
- Requires `svelte-kit sync` as a build/`prepare` step — a generated-code layer between the source and the type checker, which complicates a clean checkout.
- Pins us further into the Svelte ecosystem, compounding the undo risk already recorded for [Svelte](./svelte.md).
- The default scaffold is SSR-first; getting to the SPA build we actually want is a configuration decision that has not yet been made.

## 4. Build-vs-buy

Partly a real build candidate, and it is worth being precise about which part. A hash- or History-API router matching paths to components is genuinely small — call it half a day for something that handles our screens, and it would be a legitimate in-house choice on the "hours not weeks" test. If routing were all we needed, building it would be the correct call under this project's principles.

What is not an hours-not-weeks item is the rest: per-route code splitting, preloading, a production build pipeline, generated route types, and a static prerender step. Estimate several weeks to reach parity, with ongoing maintenance against Vite majors. That is what buying wins here.

The honest summary: we are paying for the build pipeline and getting a router thrown in, not the reverse. If SvelteKit's build story ever stops being worth it, the router is the cheap part to replace.

## 5. Risk

### Undo risk — high

SvelteKit determines the directory layout (`src/routes`), file naming (`+page.svelte`, `+layout.svelte`), navigation API, and build configuration. Every route file is coupled to it. Removing it means restructuring the entire client and replacing the build pipeline, even if Svelte components themselves survive mostly intact. Mitigant: the client is greenfield, so the cost of reversing is near zero *right now* and rises steadily from here. If the SPA-vs-SSR question is going to be settled, settling it before the routes are built out is far cheaper than after.

### Security risk — low

MIT, first-party, actively maintained, no known outstanding CVEs, dependencies are small and well-scrutinised. Two caveats specific to us. First, if any server-side SvelteKit feature is ever used, it becomes a second authentication and authorisation surface that must be kept consistent with the C# backend — the mitigation is the "no server-side SvelteKit" constraint above, which is a discipline, not an enforced control. Second, SvelteKit's fast minor cadence means `^2.63.0` will pull unreviewed minors; the `bun.lock` file pins exact resolutions, so this is only a risk at the moment the lockfile is updated.
