# DTO contract — adding DTOs

The socket protocol's data shapes are defined once, in one XML file, and generated into both C# and TypeScript. This document is the **authoritative** reference for that contract. The earlier proposal lives in [../libraries/dto-xml-contract.md](../libraries/dto-xml-contract.md); where they disagree, this document and the code win.

## Quick reference

- **Single source of truth:** [`types.xml`](../../types.xml) at the repository root.
- **C# DTOs are generated at build time** by a Roslyn source generator into the `Backend.Dtos` namespace (`Dto.g.cs`). Never write a DTO class by hand.
- **TypeScript is generated on demand** by a CLI tool, in two flavours: `-t Ts` for interfaces, `-t TsSchema` for a runtime description of the contract (see [Generation mechanics](#6-generation-mechanics)).
- **Workflow to add a request/response/event:** edit `types.xml`, rebuild, done (C#). For the frontend, also run the CLI to regenerate the `.ts` (the output is git-ignored).
- **Naming:** the emitters append suffixes — `name="Foo"` becomes `FooDto`, `FooRequest`, `FooResponse`, or `FooEvent`. Do **not** write the suffix in the XML name.
- **Every `<Request>` must contain exactly one `<Response>`** child (possibly empty).

| If you want to… | Do this in types.xml |
|---|---|
| Add a plain payload shape | `<Dto name="...">` with `<Property>` children |
| Add a client→server call | `<Request name="...">` with a `<Response>` child |
| Add a server→client notification | `<Event name="...">` with `<Property>` children |
| Add a standalone response (e.g. `Error`) | Top-level `<Response name="...">` |
| Declare item stats/tool slots content | `<Item>` / `<Skill>` (seeded into `ToolService`, see [Generated output details](#4-generated-output-details)) |

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
- Top-level element order is preserved in the generated file. An unrecognized top-level element name is a **parse error** (`Generators/Core/Parser.cs:71-73`).

### 2.2 `<Property>` attributes

| Attribute | Required | Values | Effect |
|---|---|---|---|
| `name` | yes | identifier | Property name. Lower-camel-cased in JSON and TS; upper-camel-cased in C#. |
| `type` | yes | `string` \| `int` \| `float` \| `Guid` \| `UserId` \| `ProfileId` \| `timestamp`, or the **base name of a declared `Dto`** (suffix omitted) or a **declared `Enum`** | A bare built-in, or a reference to a custom type (see below). Case-insensitive for the built-ins only. |
| `multiple` | no | `true` | Emits an array type (`T[]`). Absent / `false` = single value. |
| `optional` | no | `true` | Emits `name?: T` in TypeScript and lets the sender omit the key. On the wire an omitted key leaves the C# property at its default — there is no null. |

Built-in type mapping (verified against the emitter):

| `type` in XML | C# | TypeScript | JSON |
|---|---|---|---|
| `string` | `string` | `string` | `"..."` |
| `int` | `int` | `number` | `123` |
| `float` | `float` | `number` | `1.5` |
| `Guid` | `Guid` | `string` | `"00000000-0000-..."` |
| `UserId` | `Guid` | `string` | `"00000000-0000-..."` |
| `ProfileId` | `Guid` | `string` | `"00000000-0000-..."` |
| `timestamp` | `long` | `number` | `1760000000000` |
| declared `Dto` (e.g. `Profile`) | `ProfileDto` | `ProfileDto` | object |
| declared `Enum` (e.g. `ItemId`) | `ItemId` | `ItemId` (a string-literal union) | `"Stone"` |

**Enums are strings on the wire, not ordinals.** The backend's serializer installs a
`JsonStringEnumConverter` (`Backend/SocketJsonSerializer.cs:17`) and the TS emitter writes
`type ItemId = 'None' | 'Stone' | ...` (`Generators/Core/TsEmitter.cs:54`), so the value sent
and received is the member's UpperCamelCase name.

Two things about enums that `types.xml` does not show:

- **Every enum gains a `None` member as its first value**, added by the generator's `Enum`
  constructor (`Generators/Core/DtoModel.cs:32-35`) whether or not the XML lists one.
- **`DropTableId` and `ActivityId` are synthesised**, not declared: each `<DropTable>` and
  `<Activity>` element appends its name to the corresponding enum
  (`Generators/Core/Parser.cs:47,52`). They can be referenced from a `Property` like any
  other enum.

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

The parser (`Generators/Core/Parser.cs:225`) matches `type` against the `PropertyType` enum's member names, case-insensitively: `string`, `int`, `float`, `Guid`, `UserId`, `ProfileId`, `timestamp`. Any other value must name a declared `<Dto>` or `<Enum>` — the parser resolves it to that type and the emitters emit a reference to the generated `{Name}Dto` (for DTOs) or `{Name}` (for enums). Consequences:

- `type="Profile"` → `ProfileDto` (requires a `<Dto name="Profile">`).
- `type="ItemId"` → `ItemId` (requires an `<Enum name="ItemId">`).
- A typo such as `type="Gud"` is a **parse error** (`ParserException`, surfaced as `DTC002` in the backend build / CLI failure) — custom types must resolve to a declared DTO or enum.
- Resolution is **order-sensitive**: the parser builds its dictionaries as it walks the document, so a `Property` can only name a `<Dto>` or `<Enum>` declared *above* it in `types.xml`.

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

The TS emitter runs on demand rather than as part of `vite build` itself: locally `Frontend/package.json`'s `generate` script (which `dev`, `build` and `check` all start with) invokes it, and the frontend image regenerates the schema inside `Frontend/Dockerfile` from its commit's `types.xml`, so no output is ever checked in (the `*.generated.ts` convention excludes it via the root `.gitignore`). Target `Cs` prints the same output the source generator produces, useful for review:

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
- The three abstract bases: `DtoBase`, `RequestBase` (with `int RequestId`), `ResponseBase` (with `int RequestId`), `EventBase` (with `int EventId` and `long Timestamp`, a Unix epoch-milliseconds value the backend stamps when it sends the event).
- `public static class DropTableData` with `public static void AddAll(DropTableService service)` — a seeder that registers every `<DropTable>` from `types.xml` into a `Backend.Services.DropTableService`: `<ItemReward item=...>` drops become `new ItemReward(count, weight, ItemId.X)`, `<TableReward table=...>` drops become `new TableReward(count, weight, DropTableId.Y)`.
- `public static class ActivityData` with `public static void AddAll(ActivityService service)` — a seeder that registers every `<Activity>` from `types.xml` into a `Backend.Services.ActivityService`: each activity becomes `service.AddActivity(ActivityId.X, new ActivityDefinition(time: Nf, rewards: [...], requirements: [...]))`. The `time` attribute is required — a float number of seconds for how long the activity takes to complete by default (`<Activity name="Stone" time="2.5">` emits `time: 2.5f`). Rewards are `<ItemReward>` / `<TableReward>` / `<XpReward>`; a missing `weight` makes the reward guaranteed, a present `weight` puts it into the activity's weighted roll (same weighted pick as a drop table). `<LevelRequirement skill="..." count="..."/>` becomes `new LevelRequirement(SkillId.X, N)`.
- `public static class ToolData` with `public static void AddAll(ToolService service)` — a seeder that registers tool/item-slot content. Every `<Item name="...">` becomes `service.AddItem(ItemId.X, new ItemDefinition(tags: [ItemTagId.Y, ...], stats: [new ItemStat(ToolStat.Z, v), ...]))`. Each `<Tag name="..."/>` child maps to an `ItemTagId` value (in declaration order — the first is the main tag); each `<Stat name="..." value="..."/>` maps to a `ToolStat` and a float. Every `<Skill name="...">` with nested `<Slot>` children becomes `service.AddSkillSlots(SkillId.X, [new SlotBinding(ItemSlotId.Y, ItemTagId.T, required), ...])`; each `<Slot name="..." required="...">` carries a nested `<Tag name="..."/>` that is the accepted tag — an item is valid in that slot when one of the item's tags equals the slot's tag (resolvable via `ToolService.GetValidItems`). A `<Skill>` with no `<Slot>` children emits no `AddSkillSlots` call (the skill still auto-registers into `SkillId`). `Item`s auto-register into the `ItemId` enum, `Skill`s into the `SkillId` enum, `ItemTag`s into an `ItemTagId` enum, and slots into an `ItemSlotId` enum, exactly like `DropTableId` / `ActivityId`. These enums are **not** declared by hand in `types.xml` — declare `<Item>` and `<Skill>` elements instead (the `<Enum>` element remains available for any other enum). Ensure `Item`/`Skill` elements appear before any DTO that references `ItemId`/`SkillId`, since property types resolve against known enums at parse time.
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
  "timestamp": 1760000000000,
  "profiles": [
    { "name": "Hero", "profileId": "2efd7f6a-..." }
  ]
}
```

`eventId` and `timestamp` are stamped by the backend at send time, per socket: the first event delivered to a connection is `eventId` 0, the next 1, and so on, and `timestamp` is the Unix epoch-milliseconds moment the event was sent.

Server→client event carrying the resulting inventory and skill state (the `ActivityEndedEvent` reports the **totals** after a completion, not deltas):

```json
{
  "$type": "ActivityEndedEvent",
  "eventId": 1,
  "activityId": "Stone",
  "items": [
    { "profileId": "2efd7f6a-...", "itemId": "Stone", "count": 4 }
  ],
  "skills": [
    { "profileId": "2efd7f6a-...", "skillId": "Mining", "xp": 10, "level": 1 }
  ]
}
```

Sending the full resulting `items`/`skills` (rather than the small reward deltas) means the client always converges on total state even if an individual reward is lost.

Sending an unknown `$type` fails deserialization; `Backend/Socket.cs` converts any message-handling exception into an `ErrorResponse` (`{ "$type": "ErrorResponse", "message": "..." }`).

## 6. Generation mechanics

- **C# (build time):** `Generators/Backend/TypesGenerator.cs` is an `IIncrementalGenerator` wired into [`Backend/Backend.csproj`](../../Backend/Backend.csproj) (lines 17-24) as an analyzer. It finds `types.xml` via `AdditionalFiles`, runs the same `Parser` + `CsEmitter` as the CLI, and emits `Dto.g.cs`. Diagnostics `DTC001` (missing file) and `DTC002` (invalid XML) fail the build.
- **TypeScript (on demand):** `Generators/Generator/Program.cs` is a `CommandLineParser` console app. Flags: `-i|--input` (required), `-t|--target Cs|Ts|TsSchema` (required), `-o|--output` (default stdout).
- **`-t TsSchema`** (`Generators/Core/TsSchemaEmitter.cs`) emits the contract as a *value* rather than as declarations — an object naming every request, its properties (wire name, kind, `multiple`, `optional`) and its response, plus every DTO and enum. TypeScript types are erased at compile time, so anything that has to reason about the protocol at runtime needs this instead of `-t Ts`. The frontend's protocol console (`Frontend/src/routes/debug/`) builds its request forms from it, and `Frontend/package.json`'s `generate` script — which `dev`, `build` and `check` all depend on — keeps the output current:

  ```powershell
  cd Frontend; bun run generate
  ```

  The emitted file annotates itself with a hand-written `ProtocolSchema` interface it imports, so a change to the emitter that the consumer does not expect fails `bun run check` rather than the page.
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

- **`requestId`/`eventId` are numeric on both sides, and `timestamp` (built-in type `timestamp`, emitted as a `long`/`number`) is a Unix epoch-milliseconds value.** The C# bases hardcode `int RequestId` / `int EventId` / `long Timestamp` and the TS emitter hardcodes `requestId: number` / `eventId: number` / `timestamp: number` (`Generators/Core/TsEmitter.cs`, `Generators/Core/CsEmitter.cs`). This matches the TS socket client ([`Frontend/src/lib/ws/client.ts`](../../Frontend/src/lib/ws/client.ts)), which assigns client-chosen numeric ids (`nextRequestId`) and expects the echoed response to carry the same number. Keep the two sides in lockstep — changing one alone breaks deserialization. The contract is strict integers: there is no `JsonNumberHandling` leniency in the backend serializer, so a quoted numeral such as `"requestId": "1"` (or `"timestamp": "1760000000000"`) fails deserialization — clients must send a plain JSON number.
- **Numeric attributes are culture-invariant.** All `<Property>`/reward/activity attribute numbers (including `float` `weight` and activity `time`) are parsed with `InvariantCulture` (`Generators/Core/Extensions/XmlElementExtensions.cs`) and emitted with it too — decimals always use `.`, regardless of OS locale.
- **No editor validation.** There is no XSD; typos in `type` values surface at parse time (DTC002 / CLI error) and other mistakes at C# build time or not at all (TS). Copy a nearby block rather than typing from memory.
- **An unrecognized top-level element throws** — `Parser.Element` ends in a `default` branch that raises `ParserException` (`Generators/Core/Parser.cs:71-73`), so a typo'd element name fails the build rather than being skipped.
- **`optional` is mandatory for partial DTOs.** Non-optional properties are emitted as `required` in generated C#, so deserializing a payload that omits them throws `JsonException` ("missing required properties"). Any DTO whose sender legitimately leaves some fields unset defaults must mark them `optional` in `types.xml`.
- **Agree on totals vs. deltas per event.** Whether an event carries incremental changes or the full resulting state is a protocol decision made per event in `types.xml` (see the `ActivityEndedEvent` example in [Wire format](#5-wire-format), which sends **total** `items`/`skills`). Don't mix the two meanings on one event.
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

# 4. The runtime schema emits, and satisfies the interface the frontend checks it against
cd Frontend; bun run check
```

## 10. Related documents

- [Socket controller endpoints](./socket-endpoints.md) — how a declared request becomes a handler.
- [HTTP controller endpoints](./http-endpoints.md) — the other kind of controller.
- [DTO XML contract decision](../libraries/dto-xml-contract.md) — why the format exists (historical proposal).
