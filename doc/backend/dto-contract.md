# DTO contract — adding DTOs

The socket protocol's data shapes are defined once, in one XML file, and generated into both C# and TypeScript. This document is the **authoritative** reference for that contract. The earlier proposal lives in [../libraries/dto-xml-contract.md](../libraries/dto-xml-contract.md); where they disagree, this document and the code win.

## Quick reference

- **Single source of truth:** [`types.xml`](../../types.xml) at the repository root.
- **C# DTOs are generated at build time** by a Roslyn source generator into the `Backend.Dtos` namespace (`Dto.g.cs`). Never write a DTO class by hand.
- **TypeScript interfaces are generated on demand** by a CLI tool (see [Generation mechanics](#6-generation-mechanics)).
- **Workflow to add a request/response/event:** edit `types.xml`, rebuild, done (C#). For the frontend, also run the CLI to regenerate the `.ts` (the output is git-ignored).
- **Naming:** the emitters append suffixes — `name="Foo"` becomes `FooDto`, `FooRequest`, `FooResponse`, or `FooEvent`. Do **not** write the suffix in the XML name.
- **Every `<Request>` must contain exactly one `<Response>`** child (possibly empty).

| If you want to… | Do this in types.xml |
|---|---|
| Add a plain payload shape | `<Dto name="...">` with `<Property>` children |
| Add a client→server call | `<Request name="...">` with a `<Response>` child |
| Add a server→client notification | `<Event name="...">` with `<Property>` children |
| Add a standalone response (e.g. `Error`) | Top-level `<Response name="...">` |
| Declare item stats/tool slots content | `<Item>` / `<ItemSlot>` / `<SkillSlots>` (seeded into `ToolService`, see [Generated output details](#4-generated-output-details)) |

## 1. What DTOs exist here

Everything that crosses the WebSocket is a DTO in `Backend.Dtos`, generated from [`types.xml`](../../types.xml). There are four kinds, distinguished by their base class:

| XML element | Generated base class | Purpose | Example in types.xml |
|---|---|---|---|
| `Dto` | `DtoBase` | Reusable value object / entity projection | `ProfileDto`, `UserDto` |
| `Request` | `RequestBase` | Client→server message; always paired with a response | `CreateProfileRequest` |
| `Response` | `ResponseBase` | Server→client reply; echoes the request's `requestId` | `CreateProfileResponse` |
| `Event` | `EventBase` | Server→client push (no client request behind it) | `ProfilesChangedEvent` |

## 2. The XML contract format

### 2.1 Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<Types>
  <Dto name="Profile">
    <Property name="Name" type="string" />
    <Property name="ProfileId" type="Guid" />
  </Dto>

  <Request name="CreateProfile">
    <Property name="Name" type="string" />
    <Response />
  </Request>

  <Request name="ListProfiles">
    <Response>
      <Property name="Profiles" type="Profile" multiple="true" />
    </Response>
  </Request>

  <Event name="ProfilesChanged">
    <Property name="Profiles" type="Profile" multiple="true" />
  </Event>

  <Response name="Error">
    <Property name="Message" type="string" />
  </Response>
</Types>
```

- The root element can be any name — [`types.xml`](../../types.xml) uses `<Types>`. Only its child elements are read.
- `Property` elements are collected from **direct** children only (a `Request`'s `<Response>` is a direct child; the response's properties live *inside* `<Response>`).
- Top-level element order is preserved in the generated file. Unknown top-level element names are silently ignored.

### 2.2 `<Property>` attributes

| Attribute | Required | Values | Effect |
|---|---|---|---|
| `name` | yes | identifier | Property name. Lower-camel-cased in JSON and TS; upper-camel-cased in C#. |
| `type` | yes | `string` \| `int` \| `float` \| `Guid`, or the **base name of a declared `Dto`** (suffix omitted) or a **declared `Enum`** | A bare built-in, or a reference to a custom type (see below). Case-insensitive for the built-ins only. |
| `multiple` | no | `true` | Emits an array type (`T[]`). Absent / `false` = single value. |

Built-in type mapping (verified against the emitter):

| `type` in XML | C# | TypeScript | JSON |
|---|---|---|---|
| `string` | `string` | `string` | `"..."` |
| `int` | `int` | `number` | `123` |
| `float` | `float` | `number` | `1.5` |
| `Guid` | `Guid` | `string` | `"00000000-0000-..."` |
| declared `Dto` (e.g. `Profile`) | `ProfileDto` | `ProfileDto` | object |
| declared `Enum` (e.g. `ItemId`) | `ItemId` | `ItemId` | number |

### 2.3 Naming rules (enforced by the emitters)

| XML | Generated C#/TS name |
|---|---|
| `<Dto name="Profile">` | `ProfileDto` |
| `<Request name="CreateProfile">` | `CreateProfileRequest` + `CreateProfileResponse` |
| `<Request name="Ping"><Response name="Pong" /></Request>` | `PingRequest` + `PongResponse` |
| `<Event name="ProfilesChanged">` | `ProfilesChangedEvent` |
| top-level `<Response name="Error">` | `ErrorResponse` |

- The response's default name is the request's base name + `Response`; override with `<Response name="...">`.
- Names are normalized through a casing splitter (`Generators/Core/StringCasing.cs`), so keep names simple PascalCase words.
- Do **not** include the suffix in the XML `name` — `name="Profile"` yields `ProfileDto`, not `ProfileDtoDto`.

### 2.4 Supported property types, precisely

The parser (`Generators/Core/Parser.cs`) matches `type` against `string`, `int`, `float`, `Guid` (case-insensitive). Any other value must name a declared `<Dto>` or `<Enum>` — the parser resolves it to that type and the emitters emit a reference to the generated `{Name}Dto` (for DTOs) or `{Name}` (for enums). Consequences:

- `type="Profile"` → `ProfileDto` (requires a `<Dto name="Profile">`).
- `type="ItemId"` → `ItemId` (requires an `<Enum name="ItemId">`).
- A typo such as `type="Gud"` is a **parse error** (`ParserException`, surfaced as `DTC002` in the backend build / CLI failure) — custom types must resolve to a declared DTO or enum.

## 3. How to add a DTO

Worked example: add a `RenameProfile` request that returns the renamed profile.

### Step 1 — declare the shapes in types.xml

Append to [`types.xml`](../../types.xml):

```xml
<Request name="RenameProfile">
  <Property name="ProfileId" type="Guid" />
  <Property name="NewName" type="string" />
  <Response>
    <Property name="Profile" type="Profile" />
  </Response>
</Request>
```

### Step 2 — rebuild the backend

```powershell
dotnet build Backend\Backend.csproj
```

The source generator reads `types.xml` (wired as an `AdditionalFile` in [`Backend/Backend.csproj`](../../Backend/Backend.csproj), line 21) and emits `RenameProfileRequest` and `RenameProfileResponse` into `Backend.Dtos`. A successful build proves the contract parsed.

### Step 3 — generate the TypeScript (frontend only)

```powershell
dotnet run --project Generators\Generator -- -i types.xml -t Ts -o Frontend\src\lib\dto.generated.ts
```

The TS emitter is **not** wired into the frontend build; run the CLI to regenerate the output (it follows the `*.generated.ts` convention, so the root `.gitignore` excludes it). Target `Cs` prints the same output the source generator produces, useful for review:

```powershell
dotnet run --project Generators\Generator -- -i types.xml -t Cs
```

### What the example generates

C# (identical to `dotnet run ... -t Cs` output):

```csharp
public sealed class RenameProfileRequest : RequestBase
{
    [JsonPropertyName("profileId")]
    public Guid ProfileId { get; init; }
    [JsonPropertyName("newName")]
    public string NewName { get; init; }
}

public sealed class RenameProfileResponse : ResponseBase
{
    [JsonPropertyName("profile")]
    public ProfileDto Profile { get; init; }
}
```

TypeScript (from `-t Ts`):

```typescript
interface RenameProfileRequest extends RequestBase
{
    profileId: string;
    newName: string;
}

interface RenameProfileResponse extends ResponseBase
{
    profile: ProfileDto;
}
```

Every generated type also gets a `[JsonDerivedType(typeof(X), nameof(X))]` registration on `DtoBase`, which is what drives the `$type` discriminator on the wire (see [Wire format](#5-wire-format)).

## 4. Generated output details

The C# emitter writes a single file, `Dto.g.cs`, into the `Backend.Dtos` namespace (`Generators/Core/CsEmitter.cs`). It contains:

- The concrete classes for every DTO/request/response/event, all `sealed`.
- `[JsonPolymorphic]` + one `[JsonDerivedType]` per generated type on `abstract class DtoBase`.
- The three abstract bases: `DtoBase`, `RequestBase` (with `int RequestId`), `ResponseBase` (with `int RequestId`), `EventBase` (with `int EventId`).
- `public static class DropTableData` with `public static void AddAll(DropTableService service)` — a seeder that registers every `<DropTable>` from `types.xml` into a `Backend.Services.DropTableService`: `<ItemReward item=...>` drops become `new ItemReward(count, weight, ItemId.X)`, `<TableReward table=...>` drops become `new TableReward(count, weight, DropTableId.Y)`.
- `public static class ActivityData` with `public static void AddAll(ActivityService service)` — a seeder that registers every `<Activity>` from `types.xml` into a `Backend.Services.ActivityService`: each activity becomes `service.AddActivity(ActivityId.X, new ActivityDefinition(rewards: [...], requirements: [...]))`. Rewards are `<ItemReward>` / `<TableReward>` / `<XpReward>`; a missing `weight` makes the reward guaranteed, a present `weight` puts it into the activity's weighted roll (same weighted pick as a drop table). `<LevelRequirement skill="..." count="..."/>` becomes `new LevelRequirement(SkillId.X, N)`.
- `public static class ToolData` with `public static void AddAll(ToolService service)` — a seeder that registers tool/item-slot content. Every `<Item name="...">` becomes `service.AddItem(ItemId.X, new ItemDefinition([new ItemStat(ToolStat.Y, v), ...]))` (each `<Stat name="..." value="..."/>` maps to a `ToolStat` and a float). Every `<ItemSlot name="...">` becomes `service.AddItemSlot(ItemSlotId.X, [ItemId.Y, ...])` from its `<ValidItem name="..."/>` children. Every `<SkillSlots skill="...">` becomes `service.AddSkillSlots(SkillId.X, [new SlotBinding(ItemSlotId.Y, required), ...])` from its `<Slot name="..." required="..."/>` children. `Item`s also auto-register into the `ItemId` enum and `ItemSlot`s into a generated `ItemSlotId` enum, exactly like `DropTableId` / `ActivityId`.
The file also carries `using Backend.Services;` for these hand-written types.

All DTO classes are `sealed` and non-partial — **you cannot extend generated types with hand-written members.** If a payload needs a field, it must be declared in `types.xml`.

To inspect `Dto.g.cs` on disk, add `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` to [`Backend/Backend.csproj`](../../Backend/Backend.csproj) — it lands under `Backend/obj/.../generated/`.

**Generated file naming convention:** Generated files that should be git-ignored use the `*.generated.*` pattern (e.g., `MyFile.generated.cs`, `types.generated.ts`). This is enforced by the root `.gitignore` (`**/*.generated.*`, matching at any depth). The C# source generator's output (`Dto.g.cs`) follows the Microsoft convention (`*.g.cs`) and lands in `obj/` which is already ignored; the TypeScript CLI output should be written to a `*.generated.ts` path if you want it auto-ignored.

## 5. Wire format

Serialization is `System.Text.Json` with polymorphism on `$type` (`Backend/Socket.cs:97-100`). The discriminator is the **exact generated class name**.

Client→server (the string keys are the lower-camel-cased property names):

```json
{
  "$type": "CreateProfileRequest",
  "requestId": 1,
  "name": "Hero"
}
```

Server→client response (note `requestId` is echoed back for correlation):

```json
{
  "$type": "CreateProfileResponse",
  "requestId": 1
}
```

Server→client event:

```json
{
  "$type": "ProfilesChangedEvent",
  "eventId": 1,
  "profiles": [
    { "name": "Hero", "profileId": "2efd7f6a-..." }
  ]
}
```

Sending an unknown `$type` fails deserialization; `Backend/Socket.cs` converts any message-handling exception into an `ErrorResponse` (`{ "$type": "ErrorResponse", "message": "..." }`).

## 6. Generation mechanics

- **C# (build time):** `Generators/Backend/TypesGenerator.cs` is an `IIncrementalGenerator` wired into [`Backend/Backend.csproj`](../../Backend/Backend.csproj) (lines 17-24) as an analyzer. It finds `types.xml` via `AdditionalFiles`, runs the same `Parser` + `CsEmitter` as the CLI, and emits `Dto.g.cs`. Diagnostics `DTC001` (missing file) and `DTC002` (invalid XML) fail the build.
- **TypeScript (on demand):** `Generators/Generator/Program.cs` is a `CommandLineParser` console app using `TsEmitter`. Flags: `-i|--input` (required), `-t|--target Cs|Ts` (required), `-o|--output` (default stdout).
- The parser, model, and emitters all live in `Generators/Core/` and are shared between the two consumers.

## 7. Rules & constraints

Enforced by the parser/emitters (violations = build error):

1. A `Request` must have exactly one `Response` child (`Generators/Core/Parser.cs:61-73`).
2. `Dto`, `Request`, `Event`, top-level `Response`, and every `Property` require a `name`; every `Property` requires a `type` (`Generators/Core/Extensions/XmlElementExtensions.cs`).
3. Custom `type` values must reference a declared `<Dto name="...">` or `<Enum name="...">` (the parser errors otherwise).
4. Generated names are unique — two declarations that map to the same class name collide in the single `Dto.g.cs`.

Conventions (not enforced — reviewer judgement):

5. A `Dto` should represent one stable shape; reuse it across requests/events instead of copying properties.
6. Keep `Property` names PascalCase words so the casing splitter produces the expected camelCase.
7. `types.xml` is the contract with the frontend — treat changes as protocol changes; add new types, don't silently rename existing ones.

## 8. Gotchas & known quirks

- **`requestId`/`eventId` are numeric on both sides.** The C# bases hardcode `int RequestId` / `int EventId` and the TS emitter hardcodes `requestId: number` / `eventId: number` (`Generators/Core/TsEmitter.cs:20-33`). This matches the TS socket client ([`Frontend/src/lib/ws/client.ts`](../../Frontend/src/lib/ws/client.ts)), which assigns client-chosen numeric ids (`nextRequestId`) and expects the echoed response to carry the same number. Keep the two sides in lockstep — changing one alone breaks deserialization. The contract is strict integers: there is no `JsonNumberHandling` leniency in the backend serializer, so a quoted numeral such as `"requestId": "1"` fails deserialization — clients must send a plain JSON number.
- **No editor validation.** There is no XSD; typos in `type` values surface at parse time (DTC002 / CLI error) and other mistakes at C# build time or not at all (TS). Copy a nearby block rather than typing from memory.
- **Unknown top-level elements are silently ignored** by the parser (`Generators/Core/Parser.cs:33-52`).
- **`GetElementsByTagName("Response")` is recursive** — the response can sit anywhere inside the request element, but keep it as a direct child.
- **The decision doc differs from reality.** [`../libraries/dto-xml-contract.md`](../libraries/dto-xml-contract.md) proposed `required="true"`, `T[]` type tokens, and a namespaced root. The implemented contract (this document) uses `multiple="true"`, bare type names, and no `required` attribute. This document and the code are authoritative.

## 9. Verification

```powershell
# 1. Contract parses and C# compiles (source generator runs here)
dotnet build Backend\Backend.csproj

# 2. Generated C# looks right
dotnet run --project Generators\Generator -- -i types.xml -t Cs

# 3. TypeScript interfaces look right (output follows *.generated.* convention for git-ignore)
dotnet run --project Generators\Generator -- -i types.xml -t Ts -o Frontend\src\lib\dto.generated.ts
```

## 10. Related documents

- [Socket controller endpoints](./socket-endpoints.md) — how a declared request becomes a handler.
- [HTTP controller endpoints](./http-endpoints.md) — the other kind of controller.
- [DTO XML contract decision](../libraries/dto-xml-contract.md) — why the format exists (historical proposal).
