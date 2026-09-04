# Deployment

How OpenIdle is built, published and run. Four things are hosted — **prod backend, prod frontend, dev backend, dev frontend** — from two Docker images published to GHCR.

## The connection rules

These are the constraints the whole design exists to satisfy:

| Client | Reaches |
|---|---|
| Prod frontend | Prod backend, and nothing else |
| Dev frontend | Dev backend by default; any developer's local backend on request |
| Local frontend (`bun run dev`) | Local backend by default; the dev backend when configured |

Two mechanisms enforce them, and they work in opposite directions:

- **Outbound** — the frontend decides which backend to dial, in `Frontend/src/lib/ws/ws-url.ts`. Production ships without `PUBLIC_ALLOW_WS_OVERRIDE`, so a `?ws=` link is inert there.
- **Inbound** — the backend decides which sites may open a socket, via `AllowedWsOrigins`. Production names the prod frontend and nothing else.

Neither alone is enough. The override switch stops a prod *user* being redirected at another backend; the origin allowlist stops another *site* driving the prod backend.

## Images

| Image | Dockerfile | Build context |
|---|---|---|
| `ghcr.io/<owner>/openidle-backend` | `Backend/Dockerfile` | repository root |
| `ghcr.io/<owner>/openidle-frontend` | `Frontend/Dockerfile` | repository root |

Both contexts are the repository root deliberately. `Backend.csproj` references `Generators/Core` and `Generators/Backend` as analyzers and `../types.xml` as an `AdditionalFile`, so a `Backend/`-only context cannot compile. The frontend image needs the root for the same reason: its generated protocol TypeScript — `src/lib/ws/dto.generated.ts`, which the whole ws protocol module re-exports, so `vite build` fails without it, and the debug console's `src/lib/debug/protocol.generated.ts` — is regenerated inside the image from `../types.xml` by a .NET generator stage, because the Bun Alpine stage has no `dotnet` and the generated files are git-ignored anyway. The single root `.dockerignore` keeps the rest of the tree out of both images.

Both are multi-stage and run as a non-root user. The backend runtime is Debian-based rather than Alpine because `SQLitePCLRaw.bundle_e_sqlite3` ships a glibc-linked native library.

To build them by hand:

```sh
docker build -f Backend/Dockerfile  -t openidle-backend  .
docker build -f Frontend/Dockerfile -t openidle-frontend .
```

### Tags

`.github/workflows/publish-images.yml` publishes both images:

| Trigger | Tags |
|---|---|
| push to `main` | `dev`, `sha-<commit>` |
| push to `release` | `prod`, `latest`, `sha-<commit>` |
| manual dispatch | as for a push to the branch it was started from |

Production therefore only moves on a deliberate push to `release`, which is what keeps the prod frontend and prod backend on versions that were released together. There is no GitHub Release and no version tag — the branch is the release — so `sha-<commit>` is what identifies the commit a prod image was built from. To ship prod, push the commit you want there: `git push origin main:release`.

GHCR packages are **private by default**. Either make the two packages public, or give each host a read-only PAT and `docker login ghcr.io` before the first pull — otherwise the redeploy webhook fires and the pull fails.

## Pipeline

```
pull request ────> Backend Build + Frontend Build          (tests only)

push to main ────> publish-images.yml
                     ├─ backend-tests   (calls Backend Build)
                     ├─ frontend-tests  (calls Frontend Build)
                     ├─ publish         both images, :dev
                     └─ redeploy        environment: dev
push to release ─>   …same, but :prod + :latest   environment: prod
```

`backend-build.yml` and `frontend-build.yml` trigger on `pull_request` and `workflow_call` only. Pushes to `main` and `release` reach them through `publish-images.yml`, so the suites run once per commit and no image is ever published from a red one.

### Redeploy webhooks

The `redeploy` job POSTs a webhook per service and then polls both health endpoints, so a container that crashes on the new image turns the run red instead of leaving it green. The webhook can be anything that accepts a POST — Watchtower's HTTP API, Portainer, Coolify — since only the secret's value changes.

Configure these under **Settings → Environments**, in an environment named `dev` and one named `prod`. The names are identical in both, so the two hosts cannot be crossed:

| Kind | Name | Purpose |
|---|---|---|
| Secret | `BACKEND_REDEPLOY_URL` | POSTed to redeploy the backend |
| Secret | `FRONTEND_REDEPLOY_URL` | POSTed to redeploy the frontend |
| Secret | `REDEPLOY_TOKEN` | Optional; sent as `Authorization: Bearer` |
| Variable | `BACKEND_HEALTH_URL` | Polled after deploy, e.g. `https://api.openidle.example/healthz` |
| Variable | `FRONTEND_HEALTH_URL` | Polled after deploy, e.g. `https://openidle.example/healthz` |

Anything unset is skipped with a notice rather than failing the run, so the pipeline is usable before the hosts exist. Give the `prod` environment a required reviewer to gate every push to `release` behind an approval, and restrict its deployment branches to `release` so nothing else can deploy to it.

## Running a host

`deploy/docker-compose.dev.yml` and `deploy/docker-compose.prod.yml` describe the two environments. Copy the relevant one plus `deploy/.env.example` (as `.env`) to the host:

```sh
docker compose -f docker-compose.dev.yml up -d
```

If backend and frontend are separate machines, put the same files on both and bring up only the service that belongs there — `docker compose -f docker-compose.dev.yml up -d backend`.

TLS and public hostnames are the reverse proxy's job. The containers speak plain HTTP on 8080 (backend) and 3000 (frontend).

### Configuration matrix

| Setting | Prod backend | Prod frontend | Dev backend | Dev frontend |
|---|---|---|---|---|
| image tag | `:prod` | `:prod` | `:dev` | `:dev` |
| `PUBLIC_WS_URL` | — | prod backend `wss://…/ws` | — | dev backend `wss://…/ws` |
| `PUBLIC_ALLOW_WS_OVERRIDE` | — | **unset** | — | `true` |
| `ORIGIN` | — | prod frontend origin | — | dev frontend origin |
| `AllowedWsOrigins__0` | prod frontend origin | — | dev frontend origin | — |
| `AllowedWsOrigins__1` | — | — | `http://localhost:5173` | — |
| volume | `/data` | — | `/data` | — |

The dev backend's second allowlist entry is what lets a developer run `bun run dev` locally against it. Production has no equivalent.

### The database

SQLite, a single file, at `/data/openidle.db` via `ConnectionStrings__Default`. It **only survives a redeploy on a mounted volume**, which matters more than usual here because a webhook-driven deploy replaces the container often. Migrations are applied on boot by `Program.cs`; there is no separate migration step.

## Pointing a frontend somewhere else

### Local frontend → dev backend

Set `PUBLIC_WS_URL` in `Frontend/.env.local` to the dev backend's endpoint and run `bun run dev`. The dev backend already allows `http://localhost:5173`.

### Dev frontend → your local backend

Open the deployed dev frontend with `?ws=` naming your backend:

```
https://dev.openidle.example/login?ws=ws://localhost:5066/ws
```

The value is remembered in `localStorage` under `openidle:ws-url`, so it survives reloads and need only be typed once. `?ws=` with no value — or `?ws=reset` — clears it and hands the client back to its own backend. Anything that is not a `ws://` or `wss://` URL is ignored with a console warning rather than breaking the client, and any override already stored stays in force.

This works because each developer's local backend has an empty `AllowedWsOrigins`, which means "allow any origin" — the deliberate default for local development.

**Browser caveat.** An `https://` page opening `ws://localhost` is mixed content. Chrome permits it, because localhost counts as a potentially-trustworthy origin; Firefox and Safari block it. If the socket refuses to open there, run the frontend locally instead and use the previous section.

**None of this works against production.** The prod frontend ships without `PUBLIC_ALLOW_WS_OVERRIDE`, so the parameter is read by nothing, and the prod backend rejects a handshake from any origin but its own.

## The origin allowlist

`AllowedWsOrigins` is bound in `Backend/Extensions/WebApplicationBuilderExtensions.cs` and passed to `UseWebSockets`. As environment variables it is an indexed array: `AllowedWsOrigins__0`, `AllowedWsOrigins__1`, and so on.

Three properties are worth knowing:

- **Empty means unrestricted.** That is ASP.NET Core's behaviour with no options at all, and it is what local development wants, since the frontend's port varies. Every deployed environment must set it explicitly — the backend logs which mode it is in at startup.
- **This is not CORS.** A WebSocket handshake is not subject to the browser's same-origin policy, so `AddCors` would do nothing here. Without the allowlist, any page anywhere could drive the backend on a visitor's behalf.
- **Only requests carrying an `Origin` header are filtered.** Browsers always send one; other clients need not. It hardens the browser attack path rather than authenticating callers, and it is not a substitute for authentication.

A rejected handshake gets **403**. To check a deployment:

```sh
curl -i -H "Origin: https://evil.example" \
     -H "Connection: Upgrade" -H "Upgrade: websocket" \
     -H "Sec-WebSocket-Version: 13" -H "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==" \
     https://api.openidle.example/ws
```

## Health endpoints

Both images expose `/healthz` returning `{"status":"ok"}`, used by their `HEALTHCHECK` and by the post-deploy poll. Both are liveness only: the frontend's says nothing about whether the backend it points at is reachable, because the frontend is serving correctly either way and conflating them would restart the wrong container.
