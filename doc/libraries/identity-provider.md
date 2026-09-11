# Identity provider

- Status: adopted
- Date: 2026-09-11
- Decided by: project owner
- Version / commit pinned: n/a (self-hosted service; Zitadel with PostgreSQL 14–18)

## 1. Problem

OpenIdle needs players to log in without us handling credentials. Players should continue with the platforms they already use (Google, GitHub, Discord, Facebook, Apple), and clients we do not ship (native/mobile) must work. The backend is ASP.NET Core over a single WebSocket; the frontend is a SvelteKit SPA; the contract is a token sent as a socket `Login` message and validated by the backend. We want this self-hosted, lightweight, free, and free of a third-party web dependency that could lock us out of our own data.

The field has been narrowed to two self-hosted candidates: **Zitadel** and **Ory** (Kratos/Hydra). This document records their profiles and the trade-off between them. Earlier survey work on hosted providers (Auth0, Clerk, WorkOS, Entra External ID, Cognito, Firebase) and heavier self-hosted ones (Keycloak, Authentik, Logto, FusionAuth, Supabase) is summarised in [§2.3](#23-the-broader-field-summary) for context.

### 1.1 The N-provider cost, precisely

Supporting N social providers is **not** exponential in code, but it is combinatorial in the way that matters:

- **State space:** a user may have linked any subset of N providers, so 2^N possible subsets. That is the abstract worst case.
- **Code per provider:** O(1) *if* the identity model is canonical — an external identity is a `(provider, subject)` pair, unique, mapping to exactly one local account, and linking is an explicit, authenticated action. Then adding provider N+1 is one connector plus a linking-policy decision.
- **Review surface:** O(N²) pairwise interactions (email collisions, de-linking the last credential, merge/dedupe), not O(2^N).
- **The genuine blow-up** is *implicit* linking — automatically merging accounts by email or any matching claim. Then every provider pair can collide, and providers that do not guarantee a verified email (GitHub, Facebook) become account-takeover vectors.

This is the single strongest argument for an IdP over DIY: it ships a considered account-linking model. It is therefore a first-class comparison criterion below, not a footnote.

## 2. Alternatives considered

### 2.1 Zitadel

A complete, OpenID Certified identity platform. One Go container plus PostgreSQL; the login UI and admin Console ship with it.

- **Database:** PostgreSQL 14–18 required. CockroachDB support is dropped from v3. **No SQLite.**
- **Footprint:** ~512 MB–1 GB RAM and under 1 vCPU at rest; 2–4 vCPU recommended to absorb password-hashing spikes. Very light apart from the database.
- **OIDC/JWT:** ID tokens are always JWTs; access tokens can be issued as JWTs when the OIDC application is configured for it (otherwise opaque + introspection). A resource-based Web Keys API publishes the JWKS; APIs may verify JWT access tokens locally by public key. Audience is expressed through project scopes (`urn:zitadel:iam:org:project:id:<id>:aud`).
- **Social connectors:** Google, Apple, GitHub, GitLab, LinkedIn, Microsoft Entra, Keycloak, and generic OAuth2/OIDC via templates.
- **Account linking:** first-class. Multiple external identities link to one Zitadel account; automatic linking by matching email/username is a configurable setting.
- **Login UI:** included and customisable (Login V2). No UI to build.
- **Model:** multi-tenant (instances → organizations → projects), event-sourced full audit trail, JavaScript "Actions", machine-to-machine tokens.
- **Deployment/parity:** self-host under AGPL-3.0, or Zitadel Cloud running the same code; a commercial license removes the AGPL for enterprises.
- **Effort for OpenIdle:** low. Point the existing OIDC/JWT validator at the Zitadel issuer/audience/JWKS, configure the app for JWT access tokens, use Zitadel's login UI for social sign-in. Add a Postgres service.

### 2.2 Ory (Kratos + Hydra)

A modular, Apache-2.0 suite. **Kratos** is identity management (registration, login, MFA, sessions, recovery, verification). **Hydra** is an OAuth 2.0 / OpenID Certified provider that issues tokens but is explicitly *not* user management — it delegates authentication to a **login and consent app you build**, which it bridges to Kratos (or any identity store) via its admin API.

- **Database:** Kratos supports SQLite, PostgreSQL, MySQL, CockroachDB. Hydra supports PostgreSQL 12+, MySQL 8+, CockroachDB, **and SQLite** (file-based, `_fk=true`). In-memory mode exists for development. **The whole stack can run on SQLite.**
- **Footprint:** Go binaries; the lightest of the candidates. A single file database makes backup, snapshot and migration trivial.
- **Tokens:** Hydra issues opaque (default) or JWT access tokens, configurable globally or per client. Ordinary JWT access tokens are verifiable locally by signature in the OSS build; the *stateless* variant that skips database persistence is an enterprise/Network feature, but it is a performance optimisation, not a prerequisite for local JWT validation.
- **Kratos-only option:** for first-party login you can skip Hydra entirely and use Kratos sessions — a session cookie for browsers, an opaque **session token** for native clients, validated by a `GET /sessions/whoami` call. Kratos can also tokenise a session into a JWT via a template. This is one SQLite service, but it is a session/introspection model rather than the OIDC/JWT-audience model the backend has today.
- **Social connectors:** Kratos OIDC method — Google, GitHub, Apple, Facebook, and any OpenID Connect provider.
- **Account linking:** supported through Kratos identities and linking flows, but more assembly is left to the integrator than in Zitadel.
- **Login UI:** headless. You build the self-service login/registration UI against Kratos' HTTP flow API; Ory Elements provides components for React/Next/vanilla, **not Svelte**. Browser vs native flows differ.
- **Deployment/parity:** self-host under Apache-2.0, or Ory Network (same APIs); the Ory Enterprise License adds guaranteed CVE patching with SLAs, zero-downtime migrations, multi-region, and enterprise-only features.
- **Security history:** Hydra carries 2026 advisories — GO-2026-4807 (SQL injection via forged pagination tokens) and GO-2026-4861 (reflected XSS via `error_hint`) — which must be checked against the pinned release. Guaranteed patching is an enterprise/Network commitment; OSS relies on community releases.
- **Effort for OpenIdle:** high, and the effort is not just a login page. It has four parts:
  1. **Kratos self-service UI — you build it.** Login, registration, settings, recovery and verification pages against Kratos' flow API. The social-only login path is small (init flow → render provider buttons → submit → session), but Ory Elements targets React/Next/vanilla, **not Svelte**, so it is hand-rolled, and the non-login flows add up.
  2. **Hydra login and consent app — you build *and run* it.** Hydra does not authenticate users; it redirects to a login endpoint you host, which authenticates against Kratos and calls Hydra's admin API to accept the challenge, plus a consent endpoint (skippable/auto-accept for a first-party app) to grant scopes. This is a small server-side service, not a page, but it is new code we would own and operate.
  3. **Multi-component wiring:** two services plus a database, identity schema, OIDC provider config, secrets, CORS/cookie domains, browser-vs-native flows, upgrades.
  4. **Our backend:** the OIDC route is trivial (issuer/audience/JWKS); the Kratos-only route instead replaces the JWT validator with a `whoami` session check and still requires the UI of part 1.
  The Kratos-only route drops part 2 entirely but keeps part 1 and changes the auth model.

### 2.3 The broader field (summary)

| Candidate | Self-host | Lightest DB | Login UI included | License | Why not the shortlist |
|---|---|---|---|---|---|
| **Zitadel** | Yes | PostgreSQL | **Yes** | AGPL-3.0 / commercial | — shortlisted |
| **Ory (Kratos+Hydra)** | Yes | **SQLite** | No (headless) | **Apache-2.0** | — shortlisted |
| Keycloak | Yes | PostgreSQL/other | Basic theme | Apache-2.0 | Heavy (Java, 1–2 GB); no first-party cloud |
| Authentik | Yes | PostgreSQL only | Yes | MIT core + EE | Heaviest to run (Python, 1–2 GB) |
| Logto | Yes | PostgreSQL | Yes | MPL-2.0 | Node/Postgres; smaller vendor |
| FusionAuth | Yes | PostgreSQL/MySQL | Yes | Source-available (not OSI) | Community feature gaps |
| Supabase Auth | Yes | PostgreSQL | Yes | Apache-2.0 | Value tied to Postgres we do not use |
| Auth0 / Clerk / WorkOS / Entra / Cognito / Firebase | **No** | — | Yes | Proprietary | Cannot self-host; per-MAU cost; lock-in |

## 3. Decision & rationale

**Adopt Zitadel**, self-hosted, as the identity provider. The comparison reduces to *turnkey versus permissive-and-embedded*, and for a small team that explicitly does not want to own auth UX, turnkey wins.

| Dimension | Zitadel | Ory |
|---|---|---|
| Components to run | 1 (Zitadel) + Postgres | Kratos (+ Hydra for OIDC) + login/consent app + UI |
| Database | PostgreSQL only | **SQLite or Postgres** |
| Login UI | **Included** | You build it |
| Account linking | First-class, configurable auto-link | Available, more manual |
| OIDC JWT for our backend | Yes (configure JWT access tokens) | Yes via Hydra (configure JWT access tokens) |
| License | AGPL-3.0 (self-host fine) | **Apache-2.0** |
| Footprint (auth process) | Light; Postgres dominates | **Lightest** |
| Integration effort | **Low** | High |
| First-party cloud escape hatch | Zitadel Cloud | Ory Network |

Zitadel is the only shortlisted option that ships the login UI, the social connectors and account linking together, so the N² linking problem of [§1.1](#11-the-n-provider-cost-precisely) and the login experience become configuration rather than code we own. The existing OIDC/JWT validator ports over with configuration only, and the app is simply set to issue JWT access tokens. Its cloud is the same codebase, so the self-hosted deployment and the hosted option are interchangeable if operations ever need to move.

The accepted costs are a mandatory PostgreSQL service and AGPL-3.0. PostgreSQL is a well-understood, robust addition and can run locally or managed. AGPL is acceptable for self-hosting: running *unmodified* Zitadel as our own login server creates no obligation to publish OpenIdle's source; the obligation attaches only if we modify Zitadel and offer it as a network service, at which point a commercial license is available.

Ory remains the documented fallback if the PostgreSQL requirement or AGPL becomes unacceptable. Its SQLite support, Apache-2.0 license and minimal footprint are genuine advantages, but its headless design pushes the login UI and the Hydra login/consent bridge back onto us — precisely the work an identity provider was adopted to remove.

### Pros

- Login UI, social connectors, MFA, recovery and account linking included and maintained.
- Existing OIDC/JWT validation ports with configuration only; no new backend code.
- Light auth process (~512 MB–1 GB RAM); one integrated stack to patch and upgrade.
- Event-sourced audit trail and multi-tenant model if the game ever needs organizations.
- Self-host and cloud run the same code, so there is no lock-in to the hosted form.

### Cons

- PostgreSQL is mandatory; Zitadel cannot use the SQLite the rest of the project uses.
- AGPL-3.0 core imposes network-copyleft if we ever modify and re-offer it.
- Heavier and more opinionated than the minimal DIY or Ory approaches.
- The login UI is Zitadel's, so deep UX changes mean working within its customization model.

## 4. Build-vs-buy

The build option is DIY per-provider OAuth. It is genuinely small for one OIDC provider, but it splits into two code shapes — standard OIDC (Google, Apple) versus OAuth2-only with a REST userinfo call (GitHub, Discord, Facebook) — and it leaves account linking, email-collision handling, MFA, recovery, and the security of the callback/token exchange to us. That is the same security-critical edge the project already chose to buy for JWT validation (see [`microsoft-identitymodel.md`](./microsoft-identitymodel.md)).

Within self-hosting, the meaningful difference is where the effort lands. Zitadel converts the N² linking problem and the login UX into configuration. Ory gives us the building blocks and the data ownership, and asks us to assemble the flow — which is defensible for a team that wants to own the login experience, and expensive for one that does not.

## 5. Risk

### Undo risk — low

Zitadel is self-hosted, speaks standard OIDC, and users are keyed by an external `(provider, subject)` identity — the token's `sub`, stored on the local user. Migrating to Ory, Keycloak or a hosted provider is an identity import plus an issuer/audience/JWKS change, not a rewrite. The lock-in is the self-hosted PostgreSQL database, which we control. The planned backend integration is a thin OIDC validation service that holds no Zitadel-specific types.

### Security risk — low

Zitadel is OpenID Certified, single-vendor-maintained, and ships as one integrated stack: authentication, login UI, token issuance and account linking are patched together on a predictable cadence (quarterly majors with a six-month support window). Running it unmodified carries no AGPL source-disclosure obligation on OpenIdle; the copyleft only attaches if we modify Zitadel and offer it as a network service. Residual risk is operational — tracking Zitadel releases and running PostgreSQL — and is materially lower than assembling and securing the equivalent Kratos/Hydra/consent-app stack ourselves.
