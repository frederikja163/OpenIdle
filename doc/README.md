# OpenIdle documentation index

This is the entry point for all project documentation. It is written for **both** human readers and AI agents: the table of contents below tells you which documents exist and which to read for a given task. Documentation can drift from code — always confirm critical claims against the source files cited.

## How to use this index

- **Humans:** skim the [Documentation map](#documentation-map), then open the document for your task.
- **AI agents:** at the start of any task in this repository, read this file, then open every document listed in the [Task lookup table](#task-lookup-table) row that matches your task. Read the full document — not just its table of contents — before editing code.
- When you add or change documentation, register it here (see [Maintaining this index](#maintaining-this-index)).

## Documentation map

### Backend — protocols & endpoints

| Document | What it answers | Read before |
|---|---|---|
| [`backend/dto-contract.md`](./backend/dto-contract.md) | How to add DTOs: the `types.xml` contract, property types, naming rules, generation (C# source generator + TS CLI), wire format, gotchas | any backend change that touches a socket payload |
| [`backend/socket-endpoints.md`](./backend/socket-endpoints.md) | How to add a new socket controller endpoint: the `[SocketController]` / `[Request]` pattern, request pipeline, state, error handling | any change that adds a game-protocol call |
| [`backend/http-endpoints.md`](./backend/http-endpoints.md) | How to add a new HTTP controller endpoint (MVC pattern under `Backend/Controllers/Http/`) | adding HTTP plumbing routes |

### Deployment & CI/CD

| Document | What it answers | Read before |
|---|---|---|
| [`deployment.md`](./deployment.md) | The four hosted environments, the two GHCR images and their tags, the GitHub Actions pipeline and its secrets, how the frontend picks a backend (`PUBLIC_WS_URL` and the `?ws=` override), and the WebSocket origin allowlist | any change to Dockerfiles, `.github/workflows/`, `deploy/`, or how a client reaches a backend |

### Proposals — planned / not-yet-implemented features

| Document | What it describes |
|---|---|
| [`proposals/tools-and-item-slots.md`](./proposals/tools-and-item-slots.md) | Planned `types.xml` additions for tools, item slots, and per-item stats (status: proposal, not implemented) |

### Libraries — dependency decisions

Every third-party dependency used or considered is documented in the library index at [`libraries/README.md`](./libraries/README.md). It is the authoritative list of what we depend on and why, and it supersedes this summary:

- **Backend:** C#/.NET, ASP.NET Core (Minimal APIs + DI), EF Core + SQLite, the in-house **DTO XML contract** (`dto-xml-contract.md`), and CommandLineParser (dev-tooling only). Full decision tables in [`libraries/README.md`](./libraries/README.md#backend).
- **Frontend:** Svelte, SvelteKit, Vite, Tailwind CSS, TypeScript, shadcn-svelte + bits-ui, ESLint, Prettier, Vitest, Playwright, Fontsource typefaces, and more — grouped into Framework/build, Styling, Components, Typefaces, Linting, Formatting, and Testing tables in [`libraries/README.md`](./libraries/README.md#frontend).
- **Open items:** unresolved decisions that need attention before further work — the adapter placeholder, an unused `@internationalized/date`, the three substituted typefaces, and the not-yet-adopted `fast-xml-parser` (see [`libraries/README.md`](./libraries/README.md#open-items)).
- **Standing notes:** security and maintenance constraints that apply repo-wide — the `eslint-config-prettier` CVE, TypeScript pinned at 6.x, the Rust-binary build chain, the shadcn-svelte supply-chain review, the bundle-weight budget, and the no-install-scripts rule (see [`libraries/README.md`](./libraries/README.md#standing-notes)).

One decision document per library, following the template in [`libraries/TEMPLATE.md`](./libraries/TEMPLATE.md). To evaluate a new library, use the project's `document-library` skill.

## Task lookup table

For AI agents: find your task, read the listed documents (fully), then the source files they cite.

| Task | Documents to read first |
|---|---|
| Add a socket request/response/event payload | [`backend/dto-contract.md`](./backend/dto-contract.md), [`libraries/dto-xml-contract.md`](./libraries/dto-xml-contract.md) |
| Add a socket endpoint handler | [`backend/socket-endpoints.md`](./backend/socket-endpoints.md), [`backend/dto-contract.md`](./backend/dto-contract.md) |
| Add an HTTP endpoint | [`backend/http-endpoints.md`](./backend/http-endpoints.md) |
| Understand the socket request pipeline | [`backend/socket-endpoints.md`](./backend/socket-endpoints.md), [`backend/dto-contract.md`](./backend/dto-contract.md) |
| Add/change an EF entity or migration | [`libraries/ef-core.md`](./libraries/ef-core.md) |
| Add/evaluate/replace a third-party dependency | [`libraries/README.md`](./libraries/README.md), [`libraries/TEMPLATE.md`](./libraries/TEMPLATE.md) (via the `document-library` skill) |
| Frontend work (components, styling, tests) | [`libraries/README.md`](./libraries/README.md) sections for the affected area, then the specific decision doc |
| Security / dependency audit | [`libraries/README.md`](./libraries/README.md) (Open items + Standing notes) |
| Planned feature (not yet implemented) — e.g. tools, item slots | [`proposals/tools-and-item-slots.md`](./proposals/tools-and-item-slots.md) |
| Change CI, an image, or how a deployment is configured | [`deployment.md`](./deployment.md) |
| Change which backend a frontend connects to | [`deployment.md`](./deployment.md) |

## Repository map (orientation)

| Path | What it is |
|---|---|
| [`Backend/`](../Backend/) | ASP.NET Core app (net10.0): socket protocol, EF Core + SQLite, services. Entry point [`Program.cs`](../Backend/Program.cs). |
| [`Frontend/`](../Frontend/) | SvelteKit app (Svelte 5, TS, Tailwind). WebSocket client in [`src/lib/ws/`](../Frontend/src/lib/ws/) (`WsClient`); protocol types are generated from `types.xml` into `src/lib/ws/dto.generated.ts` by `gen:dto`, which `dev`/`build`/`check` run first — so a .NET SDK is required for frontend work. |
| [`Generators/`](../Generators/) | DTO pipeline: [`Core/`](../Generators/Core/) parser + emitters, [`Backend/`](../Generators/Backend/) Roslyn source generator, [`Generator/`](../Generators/Generator/) CLI for the TS output. |
| [`types.xml`](../types.xml) | Single source of truth for all socket DTOs. |
| [`deploy/`](../deploy/) | Compose files and the env template the hosts run. See [`deployment.md`](./deployment.md). |
| [`.github/workflows/`](../.github/workflows/) | CI (per-PR build + test) and CD (GHCR publish + redeploy webhooks). |
| [`doc/libraries/`](./libraries/) | Library decision documents + index + template. |
| [`doc/proposals/`](./proposals/) | Proposals for planned, not-yet-implemented features. |

## Maintaining this index

- Add a new document to the [Documentation map](#documentation-map) and a row to the [Task lookup table](#task-lookup-table) when you create one.
- The library index at [`libraries/README.md`](./libraries/README.md) is maintained by the `document-library` skill — keep the summary here in sync but defer to it as the source of truth.
- Prefer updating an existing document over creating a new one; the index should stay short enough to scan in one pass.
