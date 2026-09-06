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

- **Outbound** — the frontend decides which backend to dial, in `Frontend/src/lib/ws/ws-url.ts`: `PUBLIC_WS_URL` for the socket and `PUBLIC_API_URL` for the backend's HTTP side (see [Build info](#build-info)). Production ships without `PUBLIC_ALLOW_WS_OVERRIDE`, so a `?ws=` link is inert there.
- **Inbound** — the backend decides which sites may open a socket, via `AllowedWsOrigins`. Production names the prod frontend and nothing else. Only the socket is gated: the backend's HTTP side (`/health`, `/version`) is public plumbing that answers any origin, since the API is meant to be publicly reachable (see [Build info](#build-info)).

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

### Build info

The same commit is also stamped *into* both images, so a running deployment can say which build it is. The `publish` job passes two build-args to each `docker build`:

| Build-arg | Value | Source |
|---|---|---|
| `GIT_COMMIT` | full SHA of the published commit | `github.sha` |
| `GIT_COMMIT_TIME` | its committer date, unix seconds | `git log -1 --format=%ct` |

The images cannot derive these themselves because `.dockerignore` drops `.git`. Each Dockerfile consumes them differently:

- **Backend** — `dotnet publish` receives them as `-p:GitCommit` / `-p:GitCommitTime`, which `Backend.csproj` stamps into the assembly as `AssemblyMetadata`. `VersionService` reads them back at startup and the `GET /version` endpoint ([`Backend/Controllers/Http/VersionController.cs`](../Backend/Controllers/Http/VersionController.cs)) reports them over HTTP.
- **Frontend** — `vite build` receives them in its environment and `vite.config.ts` inlines them into the bundle as `__OPENIDLE_BUILD__`. Deliberately not a `PUBLIC_*` variable: those describe where an image is deployed and are read at run time, whereas this describes the image itself and must not change after the build. The frontend's own `GET /version` ([`Frontend/src/routes/version/+server.ts`](../Frontend/src/routes/version/+server.ts)) reports the same values in the same shape.

Both images therefore answer `GET /version` with `{"commit": "<full sha>", "commitTime": <epoch ms>}`, so one curl per host says which commit is running. The backend's HTTP endpoints answer any origin (`Access-Control-Allow-Origin: *`): they are public, read-only plumbing, and a browser could not read the version footer's cross-origin fetch without that header. Only the WebSocket handshake is origin-gated, by [the origin allowlist](#the-origin-allowlist).

The version footer on the login, profiles and debug pages shows both — the bundle's own build, and the build of whichever backend the client points at — as `YYYY-MM-DD HH:MM:SS <short sha>` in UTC. The backend half is fetched from `GET /version` under `PUBLIC_API_URL`, the HTTP base of the backend the frontend is deployed against (`https://api.openidle.example`, no trailing path beyond any prefix the backend is mounted under). The variable is optional: when it is unset the base is derived from `PUBLIC_WS_URL` by swapping `ws://`/`wss://` for `http://`/`https://` and dropping the final path segment (so `wss://host/api/ws` implies `https://host/api`), which is right whenever the proxy exposes both sides of the backend at one host. A `?ws=` override — or a change on the debug page — always derives the same way from the override, because it names a whole backend and the configured API URL belongs to the one just overridden away; the version fetch therefore moves with the socket. Whatever the reverse proxy forwards for the socket, it must forward `/version` (like `/health`) to the backend too, or the footer reads `unavailable` while the game works. The footer asks once per backend when it mounts, and again each time the socket opens, since a reconnect may have reached a redeployed backend. Builds outside CI (`bun run dev`, `dotnet run`, a plain `docker build`) carry no values and read `local`. To reproduce the CI values by hand:

```sh
docker build -f Backend/Dockerfile -t openidle-backend \
  --build-arg GIT_COMMIT=$(git rev-parse HEAD) \
  --build-arg GIT_COMMIT_TIME=$(git log -1 --format=%ct) .
```

One consequence worth knowing: the footer asks the backend over HTTP, so `/login` still opens no socket on page load — the socket opens at the first sign-in (or when the debug console dials).

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
| Variable | `BACKEND_HEALTH_URL` | Polled after deploy, e.g. `https://api.openidle.example/health` |
| Variable | `FRONTEND_HEALTH_URL` | Polled after deploy, e.g. `https://openidle.example/health` |

Both health paths are `/health`. Earlier revisions documented `/healthz` (and the backend image polled it while its controller already served `/health`, so the backend container was permanently unhealthy); a `BACKEND_HEALTH_URL`, `FRONTEND_HEALTH_URL` or reverse-proxy rule still ending in `/healthz` must be updated.

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
| `PUBLIC_API_URL` | — | prod backend `https://…` (optional, see [Build info](#build-info)) | — | dev backend `https://…` (optional) |
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

Set `PUBLIC_WS_URL` in `Frontend/.env.local` to the dev backend's endpoint (and `PUBLIC_API_URL` to its HTTP base, unless deriving it from the socket address is right) and run `bun run dev`. The dev backend already allows `http://localhost:5173`.

### Dev frontend → your local backend

Open the deployed dev frontend with `?ws=` naming your backend:

```
https://dev.openidle.example/login?ws=ws://localhost:5066/ws
```

The value is remembered in `localStorage` under `openidle:ws-url`, so it survives reloads and need only be typed once. `?ws=` with no value — or `?ws=reset` — clears it and hands the client back to its own backend. Anything that is not a `ws://` or `wss://` URL is ignored with a console warning rather than breaking the client, and any override already stored stays in force. The version footer follows the override too: it asks `http://localhost:5066/version`, derived from the override, rather than the deployment's `PUBLIC_API_URL`.

This works because each developer's local backend has an empty `AllowedWsOrigins`, which means "allow any origin" — the deliberate default for local development.

**Browser caveat.** An `https://` page opening `ws://localhost` is mixed content. Chrome permits it, because localhost counts as a potentially-trustworthy origin; Firefox and Safari block it. If the socket refuses to open there, run the frontend locally instead and use the previous section.

**None of this works against production.** The prod frontend ships without `PUBLIC_ALLOW_WS_OVERRIDE`, so the parameter is read by nothing, and the prod backend rejects a handshake from any origin but its own.

## The origin allowlist

`AllowedWsOrigins` is bound in `Backend/Extensions/WebApplicationBuilderExtensions.cs` and passed to `UseWebSockets`. As environment variables it is an indexed array: `AllowedWsOrigins__0`, `AllowedWsOrigins__1`, and so on.

Three properties are worth knowing:

- **Empty means unrestricted.** That is ASP.NET Core's behaviour with no options at all, and it is what local development wants, since the frontend's port varies. Every deployed environment must set it explicitly — the backend logs which mode it is in at startup.
- **This is not CORS.** A WebSocket handshake is not subject to the browser's same-origin policy, so `AddCors` would do nothing here. Without the allowlist, any page anywhere could drive the backend on a visitor's behalf. The HTTP endpoints are the other way round: they carry a permissive CORS policy on purpose, because they are public and hold nothing a visitor's browser could be tricked into leaking (see [Build info](#build-info)).
- **Only requests carrying an `Origin` header are filtered.** Browsers always send one; other clients need not. It hardens the browser attack path rather than authenticating callers, and it is not a substitute for authentication.

A rejected handshake gets **403**. To check a deployment:

```sh
curl -i -H "Origin: https://evil.example" \
     -H "Connection: Upgrade" -H "Upgrade: websocket" \
     -H "Sec-WebSocket-Version: 13" -H "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==" \
     https://api.openidle.example/ws
```

## Health endpoints

Both images expose `/health` returning `{"status":"ok"}`, used by their `HEALTHCHECK` and by the post-deploy poll. Both are liveness only: the frontend's says nothing about whether the backend it points at is reachable, because the frontend is serving correctly either way and conflating them would restart the wrong container. Both also expose `/version` (see [Build info](#build-info)) for the question the health check deliberately does not answer: which build is this?
