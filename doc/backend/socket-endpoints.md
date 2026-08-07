# Socket controller endpoints

All game protocol calls travel over a single WebSocket. A "socket endpoint" is a method on a `[SocketController]` class that handles one request type. Endpoints are discovered by reflection at startup — **no registration code is needed** beyond declaring the request type in `types.xml` and writing the method.

## Quick reference

- **Where:** any class in `Backend/Controllers/` decorated with `[SocketController]` (current example: [`UserController.cs`](../../Backend/Controllers/UserController.cs)).
- **Signature:** a `public` instance method marked `[Request]` with **exactly one parameter** whose type derives from `RequestBase`. The class must derive `SocketControllerBase`; methods usually return `Task`.
- **Dispatch key:** the parameter's type (e.g. `CreateProfileRequest`), *not* the method name.
- **Reply:** call `await RespondAsync(new FooResponse { ... })` — this sets the response's `requestId` to the request's.
- **To add an endpoint:**
  1. Declare the request (+response) in [`types.xml`](../../types.xml) (see [dto-contract](./dto-contract.md)).
  2. Write the `[Request]` method (or a new `[SocketController]` class).
  3. If it uses a new service, register that service in [`Program.cs`](../../Backend/Program.cs).
  4. Build.

## 1. How the pipeline works

```
WebSocket client
   │  JSON frame, e.g. { "$type": "CreateProfileRequest", ... }
   ▼
WsController (Backend/Controllers/Http/WsController.cs)  ── accepts the WS handshake at GET /ws
   ▼
Socket (Backend/Socket.cs)  ── one instance per connection; parses $type and raises MessageReceived
   ▼
SocketRegistryService (Backend/Services/SocketRegistryService.cs)  ── fan-out of socket events
   ▼
SocketEndpointService (Backend/Services/SocketEndpointService.cs)  ── hosted service; dispatches by request type
   ▼
[SocketController] method, e.g. UserController.CreateProfile(CreateProfileRequest)
   ▼
await RespondAsync(new CreateProfileResponse())  ── Socket sends the response frame back
```

Key files:

| File | Role |
|---|---|
| [`Backend/Controllers/Http/WsController.cs`](../../Backend/Controllers/Http/WsController.cs) | The only HTTP endpoint; accepts the WebSocket handshake at `GET /ws`. |
| [`Backend/Socket.cs`](../../Backend/Socket.cs) | Per-connection state (`User`, `Profile`), message loop, send/close. |
| [`Backend/SocketControllerBase.cs`](../../Backend/SocketControllerBase.cs) | Base class exposing `Socket`, `User`, `Profile`, `Request`, and `RespondAsync`. |
| [`Backend/Services/SocketRegistryService.cs`](../../Backend/Services/SocketRegistryService.cs) | Bridges per-socket events to the endpoint service. |
| [`Backend/Services/SocketEndpointService.cs`](../../Backend/Services/SocketEndpointService.cs) | Resolves the request type to handlers, invokes them in a DI scope. |
| [`Backend/Extensions/WebApplicationBuilderExtensions.cs`](../../Backend/Extensions/WebApplicationBuilderExtensions.cs) | `AddSocketControllers()` + `MapSocketControllers()` — reflection-based discovery. |

The reflection happens once, in `MapSocketControllers()` ([`WebApplicationBuilderExtensions.cs`](../../Backend/Extensions/WebApplicationBuilderExtensions.cs), lines 24-41): it scans the executing assembly for public classes with `[SocketController]`, then for each public instance method with `[Request]`, and registers the endpoint keyed by its single parameter type. `SocketEndpointService.TryRegisterEndpoint` ([`SocketEndpointService.cs`](../../Backend/Services/SocketEndpointService.cs), lines 82-105) validates the signature and **throws at startup** if it is wrong.

When a request arrives, `SocketEndpointService` ([`SocketEndpointService.cs`](../../Backend/Services/SocketEndpointService.cs), lines 44-80) creates a DI scope, constructs the controller via `ActivatorUtilities.CreateInstance`, sets its `Context` (which carries `Socket`/`Request`), invokes the method, and awaits every returned `Task`. Multiple controllers may handle the same request type; all handlers run concurrently.

## 2. The controller contract

Rules enforced by `TryRegisterEndpoint` (violations throw at startup):

1. The class must be `public` and carry `[SocketController]` (discovery uses `GetExportedTypes`).
2. The method must be `public`, an instance method, and carry `[Request]`.
3. The method must take **exactly one** parameter.
4. That parameter must derive from `RequestBase` — i.e. it must be a request type generated from `types.xml`.

Rules implied by the code (violations cause runtime failures, not clean errors):

5. The class must derive `SocketControllerBase` — the dispatcher only sets `Context` when the constructed controller is one ([`SocketEndpointService.cs`](../../Backend/Services/SocketEndpointService.cs), lines 61-64).
6. The class should be `sealed` and the method should return `Task` (the async pattern; only `Task` results are awaited).
7. `Request` parameters must be constructed by the dispatcher via the DI container, so any service the controller's constructor asks for must be registered in [`Program.cs`](../../Backend/Program.cs).

The canonical shape:

```csharp
using Backend.Attributes;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class ProfileController(ProfileService profileService) : SocketControllerBase
{
    [Request]
    public async Task RenameProfile(RenameProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(User);
        await profileService.RenameProfileAsync(User, request.ProfileId, request.NewName);
        await RespondAsync(new RenameProfileResponse());
    }
}
```

## 3. Step-by-step: add a new socket endpoint

Worked example: a `RenameProfile` call (continues the `RenameProfile` request declared in [dto-contract](./dto-contract.md)).

### Step 1 — declare the request and response

Already done in [dto-contract](./dto-contract.md#3-how-to-add-a-dto):

```xml
<Request name="RenameProfile">
  <Property name="ProfileId" type="Guid" />
  <Property name="NewName" type="string" />
  <Response>
    <Property name="Profile" type="Profile" />
  </Response>
</Request>
```

### Step 2 — add (or extend) a service

Domain logic lives in `Backend/Services/`, not in the controller. Controllers stay thin: validate the session, delegate, respond. Add a method to an existing service or create one:

```csharp
// Backend/Services/ProfileService.cs
internal async Task<Profile> RenameProfileAsync(User user, Guid profileId, string newName)
{
    await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
    Profile profile = await GetProfileAsync(user, profileId);
    // validate + apply + save...
    return profile;
}
```

### Step 3 — write the `[Request]` method

Add to an existing `[SocketController]` class or create a new one in `Backend/Controllers/`:

```csharp
using System;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class ProfileController(ProfileService profileService) : SocketControllerBase
{
    [Request]
    public async Task RenameProfile(RenameProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(User);
        Profile profile = await profileService.RenameProfileAsync(User, request.ProfileId, request.NewName);
        await RespondAsync(new RenameProfileResponse() { Profile = profile.ToDto() });
    }
}
```

The entity→DTO projection is a `ToDto()` method on the entity (see [`Profile.cs`](../../Backend/Database/Entities/Profile.cs), lines 18-25).

### Step 4 — register any new service

Only if the controller takes a service that is not registered yet. [`Program.cs`](../../Backend/Program.cs) (lines 15-16) registers singletons explicitly:

```csharp
builder.Services.AddSingleton<ProfileService>();
```

Controllers themselves are **never** registered — discovery is reflection-based.

### Step 5 — build and verify

```powershell
dotnet build Backend\Backend.csproj
```

Startup validation: run the backend once — `TryRegisterEndpoint` will throw immediately on a malformed handler:

```powershell
dotnet run --project Backend
```

## 4. State available to a handler

Exposed by `SocketControllerBase` ([`SocketControllerBase.cs`](../../Backend/SocketControllerBase.cs), lines 10-31):

| Member | Type | Meaning |
|---|---|---|
| `Socket` | `Socket` | The connection that sent this request. |
| `User` | `User?` | Session user, set by `UserService.SignIn` (e.g. `LoginAsTestUser`). **Null before login.** |
| `Profile` | `Profile?` | Selected profile, set by `ProfileService.SelectProfileAsync`. **Null before selection.** |
| `Request` | `RequestBase` | The raw request (same object as the typed parameter). |

Both `User` and `Profile` are per-connection state held on the `Socket`; they are not persisted unless a service writes them to the database. Guard on them with `ArgumentNullException.ThrowIfNull(...)` before use — most game actions require a logged-in user with a selected profile.

## 5. Responding and error handling

- **Reply exactly once.** There is no enforcement; calling `RespondAsync` twice sends two frames.
- `RespondAsync` copies `Request.RequestId` onto the response before sending ([`SocketControllerBase.cs`](../../Backend/SocketControllerBase.cs), lines 27-31), so clients correlate replies by `requestId`.
- **Errors:** exceptions are mapped to a single `ErrorResponse` frame, never a raw exception message. Controllers and services throw `BackendException` ([`BackendException.cs`](../../Backend/BackendException.cs)) with a deliberately client-safe message to reject a request; `Socket` ([`Socket.cs`](../../Backend/Socket.cs), lines 115-122) sends that message as the `ErrorResponse.Message`. Any other exception — including deserialization failures — becomes the generic `"Internal server error."` message, with full handler-exception details logged server-side by `SocketEndpointService` ([`SocketEndpointService.cs`](../../Backend/Services/SocketEndpointService.cs), lines 75-79). There is no structured error payload today — the message text is the contract. Throwing is the intended way to reject a request.
- **Events:** a handler can push a server-initiated `EventBase` via `Socket.SendEventAsync(...)`. There is currently **no broadcast mechanism** — `SocketRegistryService` does not track open connections, so an endpoint can only reach its own socket. Building broadcast/rooms is a deliberate future feature, not a missing bug.

## 6. Gotchas

- **Dispatch is by parameter type, not method name.** Rename the method freely; the name is just documentation. But every request type must map to a declared `Request` in `types.xml`, and the parameter type must be the generated class.
- **Signature mistakes fail at startup, not compile time.** The reflection has no compile-time knowledge; the error surfaces when `MapSocketControllers()` runs.
- **Constructors are resolved from DI.** Register every service a controller depends on. The dispatcher's `ActivatorUtilities.CreateInstance` resolves from a fresh scope per message.
- **Message size is capped.** `Backend/Socket.cs:47-54` throws `NotImplementedException` for frames over 1 KiB. Large payloads will need the message-buffering support to be built first.
- **Binary frames are unsupported** (`Backend/Socket.cs:62`).

## 7. Verification

```powershell
# Compile + source-generate the DTOs
dotnet build Backend\Backend.csproj

# Start the backend (fails fast on a bad endpoint signature)
dotnet run --project Backend
```

Manual smoke test against a socket client (the server listens on `http://localhost:5066` in the `http` launch profile):

```json
{ "$type": "CreateProfileRequest", "requestId": "req-1", "name": "Hero" }
```

expected reply:

```json
{ "$type": "CreateProfileResponse", "requestId": "req-1" }
```

## 8. Related documents

- [DTO contract — adding DTOs](./dto-contract.md) — declaring the `Request`/`Response` shapes.
- [HTTP controller endpoints](./http-endpoints.md) — the `/ws` handshake and other HTTP endpoints.
- [DTO XML contract decision](../libraries/dto-xml-contract.md) — rationale for the generated-DTO approach.
