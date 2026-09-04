---
name: add-endpoint
description: Use when adding a new socket endpoint (game-protocol call) to the OpenIdle backend - trigger words include "add an endpoint", "socket endpoint", "new request", "request/response pair", "controller method", "[Request]", "handler". Covers the full flow declare the request/response in types.xml, implement the domain logic in a service, expose it through a [SocketController] [Request] method. Use ONLY for socket endpoints; HTTP/MVC routes under Backend/Controllers/Http/ do not use types.xml and are out of scope (see doc/backend/http-endpoints.md).
---

# Adding a socket endpoint

An endpoint is one client→server call over the WebSocket. Three pieces, always in this order:

1. **Contract** — the request/response shapes in [`types.xml`](../../../types.xml).
2. **Logic** — the domain behavior in a service under `Backend/Services/`.
3. **Wiring** — a thin `[Request]` method on a `[SocketController]`.

## Step 0 — read the docs first

Per the `consult-docs` skill, read `doc/backend/dto-contract.md` and `doc/backend/socket-endpoints.md` in full before editing, and confirm their claims against the cited source files.

## Step 1 — declare the contract in types.xml

Add a `<Request>` element (copy a neighboring block; there is no editor validation):

```xml
<Request name="RenameProfile">
  <Property name="ProfileId" type="Guid" />
  <Property name="NewName" type="string" />
  <Response>
    <Property name="Profile" type="Profile" />
  </Response>
</Request>
```

Rules enforced by the parser (`Generators/Core/Parser.cs`):

- Every `<Request>` must contain exactly one `<Response>` child.
- Do **not** write suffixes in `name` — `name="RenameProfile"` generates `RenameProfileRequest` + `RenameProfileResponse`.
- `type` is a built-in (`string`, `int`, `float`, `Guid`, ...) or the base name of a declared `<Dto>`/`<Enum>`; anything else fails the build with DTC002.
- Reuse an existing `<Dto>` instead of duplicating its properties.

The C# classes are generated at compile time by the Roslyn source generator into `Backend.Dtos` (`Dto.g.cs`). Never write a DTO class by hand.

## Step 2 — implement the logic in a service

Domain logic lives in `Backend/Services/`, never in the controller. Extend an existing service or create one:

```csharp
// Backend/Services/ProfileService.cs
internal async Task<Profile> RenameProfileAsync(Guid userId, Guid profileId, string newName)
{
    await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
    // validate + apply + save...
}
```

Services take ids (`Guid userId`, `Guid profileId`), not entity objects. If you created a new service, register it in `Backend/Program.cs`:

```csharp
builder.Services.AddSingleton<ProfileService>();
```

Controllers are resolved via DI per message; an unregistered dependency fails at dispatch time, not compile time.

## Step 3 — write the controller method

Add a method to an existing `[SocketController]` in `Backend/Controllers/` or create a new one (see `UserController.cs` for the canonical shape):

```csharp
[SocketController]
public sealed class ProfileController(ProfileService profileService) : SocketControllerBase
{
    [Request]
    public async Task RenameProfile(RenameProfileRequest request)
    {
        Profile profile = await profileService.RenameProfileAsync(UserId, request.ProfileId, request.NewName);
        await RespondAsync(new RenameProfileResponse() { Profile = profile.ToDto() });
    }
}
```

Contract details that fail silently or at startup if wrong:

- Class: `public`, `sealed`, derives `SocketControllerBase`, carries `[SocketController]`.
- Method: `public` instance method carrying `[Request]`, returns `Task`, takes **exactly one** parameter whose type derives from `RequestBase` (i.e. a generated request type).
- Dispatch is keyed by the parameter type, not the method name.
- Discovery failures (wrong visibility/attributes) are **silently excluded**; signature violations throw at startup when `MapSocketControllers()` runs.
- Reply exactly once via `await RespondAsync(...)` — it copies `RequestId` onto the response automatically.
- Reject bad input by throwing `BackendException` with a client-safe message; it becomes an `ErrorResponse`. Anything else becomes a generic `"Internal server error."`.
- Session state available on the base class: `UserId`, `ProfileId` (both throw before login/profile selection), `Socket`, `Request`.

## Step 4 — verify

```powershell
dotnet build Backend\Backend.csproj   # parses types.xml, generates DTOs, compiles
dotnet run --project Backend          # startup validation throws on a malformed handler
```

Smoke test over WebSocket (`http` launch profile → `ws://localhost:5066/ws`):

```json
{ "$type": "RenameProfileRequest", "requestId": 1, "profileId": "...", "newName": "Hero" }
```

Only regenerate TypeScript when the task explicitly asks for frontend work:

```powershell
dotnet run --project Generators\Generator -- -i types.xml -t Ts -o Frontend\src\lib\dto.generated.ts
```

## Related

- `extend-types-xml` skill — when the change needs new grammar (a new attribute or tag in the contract format itself), not just new declarations.
