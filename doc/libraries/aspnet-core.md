# ASP.NET Core (Minimal APIs + DI)

- Status: adopted
- Date: 2026-08-02
- Decided by: project owner
- Version / commit pinned: ASP.NET Core 10 (LTS), ships with .NET 10, 10.0.10

## 1. Problem

The backend needs to accept and manage client connections and give us infrastructure for wiring the game together: an HTTP/WebSocket server, a dependency injection container, configuration, and logging. Without this we would have to hand-roll a network server, a DI container, routing, middleware, HTTPS handling, and WebSocket management. Important framing correction: **ASP.NET Core is not a third-party dependency** — it is a first-party part of the .NET 10 platform we already adopted. This decision is about *which slice of the platform's web stack to use*, not whether to add a new dependency. Decision criteria: support real-time connections (WebSockets now, HTTP/REST later for public APIs), keep weight minimal, stay within the platform, and avoid third-party add-ons.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **ASP.NET Core Minimal APIs + DI** (chosen) | 10.0.x, part of platform | WebApplication builder, built-in DI/config/logging, built-in WebSockets middleware, native validation, Kestrel server | Microsoft, aligned with .NET LTS patches | MIT | High: first-party, covers all needs, no new dependency |
| Full ASP.NET Core MVC | 10.0.x | Controllers, Razor views, full page surface | Microsoft | MIT | Low: page/view machinery an idle game never uses |
| SignalR (first-party) | 10.0.x | Realtime framework over WebSockets: transport fallback, reconnection, groups, RPC hub model | Microsoft, actively maintained | MIT | Medium: Microsoft's default recommendation, but an abstraction we don't need yet (see §3) |
| Third-party micro-framework (FastEndpoints, Carter, NancyFX) | varies | Endpoint/task abstractions on top of ASP.NET Core | Community | varies (MIT mostly) | Low: adds a third-party dependency to solve what the platform already solves |
| DI-only + custom hosting (raw Kestrel/HttpListener) | n/a | Use only Microsoft.Extensions DI + host a hand-rolled HTTP/WS server | n/a (in-house) | n/a | Low: reimplements routing/middleware/WebSockets in-house |
| No framework / plain BCL sockets | n/a | Zero framework, hand-written sockets | n/a (in-house) | n/a | Rejected: no connections at all is not our requirement |

Why the others lost: MVC is all surface we won't touch. SignalR is deferred, not rejected — it's first-party and can be layered on later. Third-party micro-frameworks violate the "as few third-party libraries as possible" rule with no benefit. In-house hosting fails the build-vs-buy test badly (§4).

## 3. Decision & rationale

Adopt **ASP.NET Core Minimal APIs + built-in DI** on .NET 10, using Kestrel as the server. This is the leanest slice of the platform that gives us everything: `WebApplicationBuilder` for hosting, `Microsoft.Extensions.DependencyInjection` for wiring, first-party config/logging, and built-in WebSocket middleware for the real-time layer. HTTP/REST endpoints for public APIs are free later via `MapGet`/`MapPost` — the same framework serves both, so the "WebSocket-first, REST-later" ordering costs nothing.

On the WebSocket-first choice: our earlier requirement round did not list real-time, and WebSocket-first is the harder of the two paths (reconnects, heartbeats, state re-sync on reconnect). We accept it because this is an *MMORPG* — live presence/chat matter — and ASP.NET Core's built-in WebSocket support (and .NET 10's `WebSocketStream`) keeps the cost low. Obligations we take on: design the wire protocol versioned from day one, handle reconnect/re-sync early, and don't let connection management leak into game logic.

Why not SignalR: Microsoft recommends it for "most apps" and it's first-party, so this is a genuine consideration. We reject it *for now* because (a) an idle MMO's realtime surface is small (presence, chat, leaderboards) and raw WebSockets keep full control of a tightly-specified binary game protocol; (b) SignalR's fallback/JSON/RPC machinery is abstraction we'd pay for without using; (c) it stays first-party and can be added non-invasively later. Documented as our escalation path if realtime features grow beyond presence/chat.

Why not a third-party framework: nothing it adds is missing from the platform, and it would be a third-party dependency for zero gain.

### Pros

- First-party: zero new dependencies, stays aligned with .NET 10 LTS security patches.
- Lean slice: hosting + routing + DI + config + logging + WebSockets without MVC/views.
- Kestrel is a battle-tested, high-performance server; HTTPS/HTTP-2 free.
- Same framework serves WebSockets now and REST later.
- DI container has scoped lifetimes, validation, and integrates with everything in the ecosystem.
- Native validation support in .NET 10 minimal APIs reduces the need for a third-party validation library later.

### Cons

- Raw WebSockets means we own the connection-management concerns (heartbeats, reconnect, message framing) that SignalR would have given us — a modest amount of in-house code.
- Committing to the framework's conventions; the DI container is the platform's, not ours to swap for another.
- WebSocket-first is the harder ordering and we've accepted reconnect/resync as early design work.

## 4. Build-vs-buy

The in-house option (DI-only + raw Kestrel/HttpListener, or plain sockets) would mean writing: an HTTP server with keep-alive and HTTPS, request routing, middleware pipeline, a DI container with scoped lifetimes, configuration and logging wiring, WebSocket accept/handshake/heartbeat/framing, and validation — a conservative estimate of **weeks of careful work**, and it's the security-critical network edge where bugs cost players' data. That clearly fails the "buildable in a short amount of time" test, and ASP.NET Core is *already in the platform we chose*, so the marginal cost of using it is effectively zero. This is the strongest possible buy case. The one in-house-able piece (a basic DI container, ~a few hundred lines) is not worth it: you'd lose scoped lifetimes, validation, and ecosystem integration for no gain.

## 5. Risk

### Undo risk — low

Confined to the hosting/connection layer. Game logic stays in separate modules, so swapping or trimming the framework later is localized, not a rewrite. Because it's first-party and the platform default, it also rides the same .NET LTS upgrade path with no external-maintenance risk. The only thing that would hurt to reverse is a wire protocol built on our WebSocket handling — hence the versioned-protocol obligation in §3.

### Security risk — low

First-party Microsoft component, the most-audited network surface in the .NET ecosystem, patched monthly on the same cadence as the platform. The real attack surface is *us*: an open WebSocket endpoint. Mitigations to follow from the start: HTTPS only, validate WebSocket origin, authenticate on connect, cap message sizes, and keep connection management in its own layer.
