# @sveltejs/adapter-auto

- Status: rejected (superseded by [@sveltejs/adapter-node](./sveltejs-adapter-node.md))
- Date: 2026-08-03, resolved 2026-08-27
- Decided by: project owner
- Version / commit pinned: 7.0.1 (declared `^7.0.1`)

## 1. Problem

[SvelteKit](./sveltekit.md) produces a build whose output shape depends on where it will run — a Node server, a static file tree, a serverless function, an edge worker. An adapter is the piece that decides which. A SvelteKit build cannot complete without one, so this is a required slot; the only question is which adapter fills it.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@sveltejs/adapter-auto** (currently installed) | 7.0.1, 0 direct deps | Detects the deployment platform at build time from CI environment variables, then downloads and delegates to the matching adapter | Maintained but slow-moving: 670K weekly downloads, latest 2026-02-12 | MIT | **Low** — it is a scaffold placeholder, not a decision. Detects nothing when the target is our own VPS |
| `@sveltejs/adapter-static` (recommended) | 3.x, small | Prerenders to plain HTML/CSS/JS. No Node process in production. Pairs with `ssr = false` for a pure SPA | Actively maintained, first-party | MIT | **High**: matches the decided architecture — C# is the only backend; the client is static assets |
| `@sveltejs/adapter-node` | 5.x, small | Emits a standalone Node server | Actively maintained, first-party | MIT | Low: introduces exactly the second backend process that [SvelteKit](./sveltekit.md) explicitly rules out |
| Platform adapters (Vercel / Cloudflare / Netlify) | various | Serverless and edge deployment | Actively maintained | MIT | Low: no such hosting is in use, and each couples the build to one vendor |
| Build in-house (custom adapter) | n/a | SvelteKit's adapter API is small and documented; a static adapter is genuinely simple | Us | n/a | Low: `adapter-static` already exists, is first-party, and is tiny. No reason |

Why `adapter-auto` loses: it solves a problem we do not have. It exists so that a freshly scaffolded project deploys without configuration when pushed to a managed host that sets recognisable CI environment variables. This project deploys to a self-hosted Linux VPS ([C# / .NET](./csharp-dotnet.md)), where no such variables exist and detection falls through to an error. The scaffold's own generated comment in `vite.config.ts` says as much: *"adapter-auto only supports some environments… If your environment is not supported, or you settled on a specific environment, switch out the adapter."*

## 3. Decision & rationale

> **Resolved 2026-08-27 — removed in favour of [@sveltejs/adapter-node](./sveltejs-adapter-node.md), not `adapter-static`.**
>
> Everything below is the original review, kept because its criticism of `adapter-auto` is exactly why it was removed and still stands. Its *recommendation* does not: it assumed the frontend would be built once and served as static files by ASP.NET Core, and the deployment that was actually built does not work that way. The frontend is now its own container, and one image serves both dev and prod with `PUBLIC_WS_URL` read at run time — which a static build cannot do, since `$env/dynamic/public` collapses to build-time substitution without a server process. The trade-off that buys, and the "second backend" objection it has to answer, are argued in the adapter-node document.
>
> The parts of section 4 below describing `adapter-static` configuration — full prerender versus `fallback: 'index.html'` plus `MapFallbackFile` — remain accurate should the project ever move back; `ssr = false` is still worth adopting on its own merits, and is recorded as an open follow-up there.

**Status was `under-review` because no decision had actually been made.** `adapter-auto` is present because `sv create` put it there, and it survives only because nothing has been deployed. It is a default, and this project's rules do not let a default stand as a decision.

The recommendation is to **replace it with `@sveltejs/adapter-static`, configured with `ssr = false`**, giving a client built to static files. `ssr = false` is only half the configuration — the other half is what `adapter-static` does with a URL it did not prerender, and the two coherent answers (full prerender, or an `index.html` fallback with a matching ASP.NET Core rewrite) are set out in section 4. That follows directly from the architectural constraint recorded in [SvelteKit](./sveltekit.md): the C# server is the only backend, so the client should compile to assets that any static host — or the ASP.NET Core process itself — can serve. It also removes a Node runtime from production entirely, which is one fewer thing to patch, monitor, and keep alive on the VPS.

Two properties of `adapter-auto` are worth flagging beyond mere redundancy. It resolves and **downloads the real adapter at build time**, meaning a production build reaches out to the npm registry for a package that is not in `bun.lock` — an unpinned dependency introduced during the build, which is both a reproducibility problem and a supply-chain one. And because detection is environment-driven, the same commit can produce different build output on different machines. Neither is acceptable for a build we expect to reproduce.

This document was revised to `rejected` on 2026-08-27, when the swap was made and the replacement documented separately — see the note at the top of this section for which replacement, and why it was not the one recommended here.

### Pros

- Zero direct dependencies; the package itself is trivially small.
- Lets the scaffold build succeed on managed hosts without any configuration — genuinely useful for its intended audience.
- First-party and MIT, so nothing is wrong with it on quality grounds.

### Cons

- Makes no decision; it defers one, and this project requires the decision to be made.
- Downloads the actual adapter at build time — an unpinned, un-lockfiled dependency resolved during a production build.
- Non-deterministic: build output depends on ambient environment variables, so builds are not reproducible across machines.
- Detects nothing on a self-hosted VPS, which is our actual target, so it will simply fail when we first deploy.
- Slowest-moving package in the frontend set (last release 2026-02-12), consistent with it being a convenience shim rather than active infrastructure.

## 4. Build-vs-buy

Neither. The correct move is not to build an adapter and not to keep this one — it is to install the first-party adapter that matches our deployment target. `@sveltejs/adapter-static` is small, maintained by the same team, and pinnable. Writing a custom adapter against SvelteKit's adapter API would be perhaps a day's work and is entirely unnecessary when the official one does exactly this.

Swapping is small, but "swap the adapter" is not by itself a complete configuration — `adapter-static` needs to be told what to do with a URL it did not prerender, and the two ways of answering that are different deployments:

- **Full prerender.** `adapter()` with no `fallback`, plus a root `+layout.ts` exporting `ssr = false` and `prerender = true`. Every route is emitted as its own `index.html` at build time and ASP.NET Core serves the tree as ordinary static files. This only works while every route is enumerable at build time; a dynamic route with unknown parameters fails the build rather than degrading.
- **SPA fallback.** `adapter({ fallback: 'index.html' })`, plus a root `+layout.ts` exporting `ssr = false` and `prerender = false`. One HTML shell is emitted and the client router resolves everything. ASP.NET Core must then rewrite any non-asset 404 back to `/index.html` — `MapFallbackFile("index.html")` after `UseStaticFiles()` — or a hard refresh on a deep link returns 404 from the server.

The game client is a long-lived authenticated session behind `(auth)`, so the SPA fallback is the likely answer and the ASP.NET Core rewrite is a required part of it, not an afterthought. Pick one before the swap and make `vite.config.ts` and `+layout.ts` agree; mixing them — `fallback` set while `prerender = true` — is the configuration that silently ships both and confuses which one is actually serving a route.

## 5. Risk

### Undo risk — low

Referenced in exactly one place: the `adapter: adapter()` call in `vite.config.ts`. Replacing it is a two-line edit plus a dependency swap. This is the cheapest change in the entire frontend dependency set — which is precisely why there is no excuse for leaving it unresolved.

### Security risk — medium

Rated above `low` specifically because of the build-time download behaviour. `adapter-auto` fetches and executes an adapter package that is not recorded in `bun.lock`, so a production build pulls and runs code the lockfile never pinned and no review ever covered. That defeats the main protection a committed lockfile provides, and it does so inside the build step, which runs with full local privileges. The package itself is clean, first-party, and has no known CVEs; the risk is structural, not a flaw in its code. Replacing it with a pinned, lockfiled `adapter-static` reduces this to `low`.
