# Microsoft.AspNetCore.Mvc.Testing (integration-test hosting)

- Status: rejected
- Date: 2026-08-04
- Decided by: project owner
- Version / commit pinned: n/a (not adopted)

## 1. Problem

The socket-broadcasting feature needs integration tests that exercise the real application: WebSocket handshake, message framing, DI, the socket registry fan-out, and the SQLite-backed controllers. The question was how to host the real `Backend` app in-process for those tests. The candidate off-the-shelf answer was `Microsoft.AspNetCore.Mvc.Testing` (the `WebApplicationFactory<TEntryPoint>` package); the alternative is an in-house harness that builds and starts the real app directly. This is a dev-time-only concern; nothing in the shipped product depends on it.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Build in-house: in-process Kestrel harness** (chosen) | n/a (zero packages) | Refactor `Program.cs` to expose `CreateApp()`/`MigrateDatabaseAsync()`; tests build the real `WebApplication`, bind an ephemeral port, and connect real `ClientWebSocket`s over TCP | n/a (we own it) | n/a | High: no new dependency, real TCP sockets, exactly mirrors the existing smoke test |
| Microsoft.AspNetCore.Mvc.Testing | 10.x, small Microsoft package | `WebApplicationFactory<Program>` + `TestServer`; in-memory HTTP/WebSocket transport (`Server.CreateWebSocketClient()`); DI override via `WithWebHostBuilder`; standard .NET integration-testing idiom | Active: Microsoft ships it with the ASP.NET Core release | Apache-2.0 | Medium: idiomatic and well-maintained, but adds a package and uses TestServer's in-memory transport instead of real sockets |
| Microsoft.AspNetCore.TestHost (only) | 10.x | `TestServer` without the `WebApplicationFactory` convenience | Active: Microsoft | Apache-2.0 | Low: still a new package, and loses the factory's lifecycle management without gaining anything |
| Subprocess harness (smoke test style) | n/a | Launch the built `Backend.dll` as a child process, connect over TCP | n/a | n/a | Low: slow, port/timing flaky, needs process management — rejected as the basis for a repeatable test suite |

Why the others lost: **Mvc.Testing** is the industry default and was seriously considered; it lost because the owner's explicit preference is to keep the dependency count as low as possible and because real TCP WebSockets (its in-memory `TestServer` transport does not use sockets) better match the "the app sometimes hangs" concern that motivated the integration tests. **TestHost alone** adds a package without the factory's benefits. **Subprocess** is what the manual smoke test already does and is too slow/flaky for the suite.

## 3. Decision & rationale

Reject `Microsoft.AspNetCore.Mvc.Testing`; build a small in-house harness in the new `tests/OpenIdle.IntegrationTests` project. The harness extracts startup into `AppHost.CreateApp(string[] args, string? connectionString)` and `AppHost.MigrateDatabaseAsync(IServiceProvider)` (a dedicated static class, keeping `Program.cs` a plain top-level-statement entry point), then starts the returned `WebApplication` on `127.0.0.1:0` (OS-assigned ephemeral port), points the connection string at a unique temp SQLite file, and lets tests drive it with real `ClientWebSocket` connections. All of the pieces (Kestrel, SQLite, `ClientWebSocket`) already exist in the project, so this adds zero dependencies. Each test gets its own app instance and DB; per-operation and per-test timeouts turn any server hang into a test failure.

### Pros

- Zero new packages — satisfies the project's "as few third-party libraries as possible" rule.
- Uses real TCP sockets end-to-end (the exact thing the manual smoke test validates), so hang/deadlock behavior in the receive/send loops is exercised for real.
- No `public partial class Program` marker or `WebApplicationFactory` reflection magic required; `AppHost` is referenced directly via the existing `InternalsVisibleTo`.
- The `AppHost.CreateApp` seam is small and genuinely useful beyond tests (it makes startup logic independently callable).
- Cheap and fully controlled: ephemeral ports, isolated temp DBs, deterministic cleanup.

### Cons

- We own the harness: startup, port discovery (`IServerAddressesFeature`), and cleanup are ~70 lines we must maintain.
- Reimplements (thinly) what `WebApplicationFactory` provides out of the box; no automatic DI override hook like `WithWebHostBuilder` — connection-string injection is handled by the `CreateApp` parameter instead.
- Controller discovery needed an explicit `.AddApplicationPart(...)` in `Program.cs` because MVC scans the entry assembly, which is the test assembly in this scenario.
- Per-test server startup (a few hundred ms) is slower than TestServer's in-memory host.

## 4. Build-vs-buy

This was genuinely close. `WebApplicationFactory` would have been a one-line fix plus a package reference; the in-house harness is ~70 lines plus a small `Program.cs` refactor. But the in-house option reuses infrastructure that already exists in the repo (Kestrel, `ClientWebSocket`, `InternalsVisibleTo`, the smoke-test knowledge) and honors the standing rule that every dependency must justify itself. The realistic build-vs-buy reading: this is on the "hours, not weeks" boundary, and the maintainers chose to own it. If the harness ever grows painful, `Mvc.Testing` remains a drop-in future option — nothing locks us out of it.

## 5. Risk

### Undo risk — low

No new dependency to remove. Reverting to `Mvc.Testing` later means deleting the two harness files, restoring the `Program.cs` seam, and deleting one test file — contained entirely to `tests/OpenIdle.IntegrationTests` plus a 5-line production-code seam. The `Program.cs` refactor is behavior-preserving and verified by the existing smoke test.

### Security risk — low

Dev-only. No third-party code is added, so there is no new supply-chain surface beyond what the Backend project already uses. The harness binds only to `127.0.0.1` on ephemeral ports and never touches untrusted input.
