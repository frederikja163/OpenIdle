# @sveltejs/adapter-node

- Status: adopted
- Date: 2026-08-27
- Decided by: project owner
- Version / commit pinned: 5.5.7 (declared `^5.5.7`)

## 1. Problem

[SvelteKit](./sveltekit.md) cannot build without an adapter, and the placeholder it was scaffolded with — [@sveltejs/adapter-auto](./sveltejs-adapter-auto.md) — detects nothing on a self-hosted target and downloads an unpinned adapter mid-build. That had to be resolved before anything could be deployed at all.

Resolving it turned out to depend on a second question that was not on the table when `adapter-auto` was first reviewed: **the frontend now ships as a Docker image, and the same image has to run in more than one environment.** Four things are hosted — prod backend, prod frontend, dev backend, dev frontend — and the frontend's only environment-specific configuration is the address of the backend it talks to (`PUBLIC_WS_URL` for the socket, `PUBLIC_API_URL` for its HTTP side), plus whether the `?ws=` developer override is live (`PUBLIC_ALLOW_WS_OVERRIDE`). *Where* those values are read decides whether one image can serve both environments or whether each needs its own build.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@sveltejs/adapter-node** (chosen) | 5.5.7, small, first-party | Emits a standalone Node server. `$env/dynamic/public` is resolved per request, so `PUBLIC_*` are real container environment variables | Actively maintained, first-party | MIT | **High** — one image, configured at run time, promotion without a rebuild |
| `@sveltejs/adapter-static` | 3.x, small, first-party | Prerenders to a static tree; no server process in production | Actively maintained, first-party | MIT | Medium — smaller and simpler, but bakes `PUBLIC_WS_URL` in at build time |
| `adapter-static`, served by ASP.NET Core | n/a | No second process at all; `UseStaticFiles` + `MapFallbackFile` | n/a | n/a | Low — collapses the frontend into the backend deployment, contradicting the four-host topology |
| `adapter-auto` (incumbent) | 7.0.1 | Detects the platform at build time | Slow-moving | MIT | None — see its own document |

Why `adapter-static` lost, despite being the recommendation recorded in [@sveltejs/adapter-auto](./sveltejs-adapter-auto.md): with no server process, `$env/dynamic/public` collapses into build-time substitution. `PUBLIC_WS_URL` becomes a Docker build argument, so `openidle-frontend:dev` and `openidle-frontend:prod` are two *different builds* of the same commit. Three consequences follow:

- Promotion becomes a rebuild rather than a retag, so the artifact tested in dev is provably not the artifact shipped to prod.
- The publish workflow doubles: one build per image per environment, and prod's build is the one that has never been exercised.
- Every future runtime knob inherits the same constraint, not just this one.

Why serving the static tree from ASP.NET Core lost: it is genuinely the smallest architecture, and it is what the earlier document assumed. But it makes the frontend not a separately deployable thing, and the requirement here is explicitly four independently hosted, independently redeployable services. It also could not serve `src/routes/health/+server.ts`, which the container healthcheck and the post-deploy gate in `.github/workflows/publish-images.yml` both use.

## 3. Decision & rationale

**Adopt `@sveltejs/adapter-node`, and keep the Node process strictly an asset-and-config server.**

This is a deliberate, scoped exception to the constraint recorded in [SvelteKit](./sveltekit.md) — *"adopt SvelteKit for routing, code splitting, and the build pipeline; treat its server-side capabilities as out of bounds"* — and it is worth being precise about what the exception covers, because that document's reasoning is still correct. The concern there is game logic: *"any game logic that leaks into the Node layer is logic a player can reach outside the C# server's validation."* Nothing here changes that. The Node layer:

- serves the built client assets,
- injects `PUBLIC_*` values into the page at request time,
- answers `/health`.

It holds no game state, reads no database, and does not talk to the C# backend at all — the browser still dials the socket directly. There are no `+page.server.ts` files, no form actions, and no remote functions, and there should continue to be none. The authoritative state still lives in exactly one place.

The concrete win is that `Frontend/Dockerfile` produces **one** image that `deploy/docker-compose.dev.yml` and `deploy/docker-compose.prod.yml` run with different environment variables, and that `PUBLIC_ALLOW_WS_OVERRIDE` — the switch that makes the `?ws=` override inert in production — is a property of the *container* rather than of the build. See [deployment](../deployment.md).

### Pros

- `PUBLIC_WS_URL`, `PUBLIC_API_URL` and `PUBLIC_ALLOW_WS_OVERRIDE` are read per request, so one image serves every environment and promotion is a retag rather than a rebuild.
- First-party, pinned in `bun.lock`, and — unlike `adapter-auto` — downloads nothing during a build.
- The emitted server is self-contained: `build/` imports only `node:` builtins, so the runtime image ships no `node_modules` at all.
- Supports `+server.ts`, which the frontend healthcheck and the post-deploy gate depend on.
- Referenced in one line of `vite.config.ts`, so it is as cheap to reverse as the adapter it replaced.

### Cons

- Puts a JavaScript runtime in production that has to be patched, monitored and restarted — precisely the cost `adapter-static` would have avoided. Mitigated by it being a bare asset server with no application code.
- Larger runtime image than a static tree behind nginx, and one more moving part per environment.
- SSR is still on by default, so pages are server-rendered even though nothing needs it. **Open follow-up:** a root `+layout.ts` exporting `ssr = false` would reduce the Node layer to asset serving and match the SPA intent in [SvelteKit](./sveltekit.md) without changing the adapter. Not done here because it is an architecture decision rather than a deployment one.
- Formally widens the "second backend" surface even though nothing uses it: keeping `+page.server.ts` out is now a convention rather than something the build makes impossible.

## 4. Build-vs-buy

Buy — and there was never a real alternative. SvelteKit's adapter API is small enough that a custom Node adapter would be perhaps a day's work, but `adapter-node` is first-party, maintained in lockstep with SvelteKit, and does exactly this. Writing one would add a maintenance burden in exchange for nothing.

## 5. Risk

### Undo risk — low

One import line in `vite.config.ts`, plus the runtime stage of `Frontend/Dockerfile` and the `PUBLIC_*` entries under `deploy/`. Moving to `adapter-static` later means accepting build-time configuration and splitting the frontend image per environment; the client code needs no change, since `resolveWsUrl()` in `Frontend/src/lib/ws/ws-url.ts` reads `$env/dynamic/public`, which degrades to build-time substitution rather than breaking. The one thing that would have to move is `src/routes/health/+server.ts`, which a static build cannot serve.

### Security risk — low

First-party, MIT, actively maintained, pinned in `bun.lock`, and it resolves nothing at build time — the specific structural problem that put `adapter-auto` at `medium`. The residual risk is operational rather than supply-chain: a long-lived Node process is now exposed to the internet, so its base image has to be kept current. `Frontend/Dockerfile` pins `oven/bun:1.3.10-alpine` for exactly that reason — the version is visible and bumped deliberately rather than drifting.
