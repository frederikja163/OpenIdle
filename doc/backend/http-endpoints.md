# HTTP controller endpoints

The backend exposes two plain HTTP routes today — `GET /health` ([`Backend/Controllers/Http/HealthController.cs`](../../Backend/Controllers/Http/HealthController.cs)) and `GET /version` ([`Backend/Controllers/Http/VersionController.cs`](../../Backend/Controllers/Http/VersionController.cs)) — plus the WebSocket handshake at `GET /ws`. The frontend image mirrors both as SvelteKit server routes (`Frontend/src/routes/health/+server.ts`, `Frontend/src/routes/version/+server.ts`). Everything else the client needs goes over that socket via [socket endpoints](./socket-endpoints.md). When you do need a plain HTTP endpoint, it is a standard ASP.NET Core MVC controller — this page records the house pattern.

## Quick reference

- **Where:** `Backend/Controllers/Http/`.
- **Pattern:** `[ApiController]` + `ControllerBase`, attribute route on each action.
- **Wiring already exists** — [`AppHost.cs`](../../Backend/AppHost.cs) calls `AddControllers()`, `AddOpenIdleCors()` and `app.UseOpenIdleCors()` / `app.MapControllers()` in `CreateApp`. You only register *services*, never controllers.
- **To add an endpoint:** write the controller class; register any new service; build.

## 1. The existing example

```csharp
// Backend/Controllers/Http/WsController.cs
[ApiController]
public sealed class WsController(SocketRegistryService socketRegistryService) : ControllerBase
{
    [HttpGet("/ws")]
    public async Task Ws()
    {
        // rejects non-WebSocket requests, accepts the handshake,
        // registers the socket, and pumps messages
    }
}
```

Notes drawn from it:

- The controller is `sealed` with a primary-constructor-injected service (resolved from DI at request time).
- The route is an attribute on the action: `[HttpGet("/ws")]`.
- `[ApiController]` + `ControllerBase` gives model binding, `ProblemDetails`, and `StatusCodes` helpers — the project's convention over minimal-API lambdas.
- The class is **not** registered anywhere; `MapControllers()` discovers it.

## 2. Step-by-step: add a new HTTP endpoint

Worked example: `GET /health` returning a small status object.

### Step 1 — create the controller

```csharp
// Backend/Controllers/Http/HealthController.cs
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Http;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok" });
    }
}
```

- `namespace Backend.Controllers.Http` — the dedicated folder/namespace for HTTP controllers (keeps them visually distinct from `[SocketController]` classes in `Backend/Controllers/`).
- Route strings must start with `/` (convention routes are not configured).

### Step 2 — inject services if needed

A controller that needs services takes them in its primary constructor, e.g. `HealthController(ProfileService profileService)`. Register anything not already registered in [`AppHost.cs`](../../Backend/AppHost.cs) (`CreateApp`, the `AddSingleton` block):

```csharp
builder.Services.AddSingleton<ProfileService>();
```

`AddControllers()` and `MapControllers()` are already in place — no new registration for the controller itself.

### Step 3 — build and verify

```powershell
dotnet build Backend\Backend.csproj
dotnet run --project Backend
```

Then:

```powershell
Invoke-RestMethod http://localhost:5066/health
```

The `http` launch profile serves `http://localhost:5066` ([`Backend/Properties/launchSettings.json`](../../Backend/Properties/launchSettings.json)).

## 3. Rules & conventions

1. Controllers live in `Backend/Controllers/Http/`, namespace `Backend.Controllers.Http`.
2. Decorate with `[ApiController]` and derive `ControllerBase`.
3. Put a route attribute on each action (`[HttpGet("...")]`, `[HttpPost("...")]`, ...). Prefer the literal `/path` form.
4. Return `IActionResult` helpers (`Ok`, `NotFound`, `BadRequest`, ...).
5. Never register the controller — only its services, in `AppHost.cs`.
6. Everything tied to game state goes through the socket, not HTTP; use HTTP for plumbing (handshake, health, static-ish concerns).
7. **HTTP endpoints are public.** The default CORS policy in `AddOpenIdleCors` answers any origin, because the HTTP side is meant to be a publicly reachable API and a frontend on another origin could not read a cross-origin `fetch` without it. `AllowedWsOrigins` gates the socket handshake only. So never put anything origin- or session-sensitive on HTTP; rule 6 already keeps the session on the socket. See [`../deployment.md`](../deployment.md).

## 4. Related documents

- [Socket controller endpoints](./socket-endpoints.md) — the primary game-protocol API.
- [DTO contract — adding DTOs](./dto-contract.md) — socket payload shapes (not used by HTTP endpoints).
