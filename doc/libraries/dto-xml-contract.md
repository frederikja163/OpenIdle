# DTO XML contract

- Status: in-house
- Date: 2026-08-04 (decided); **implemented 2026-08-06**
- Decided by: project owner
- Version / commit pinned: format as-implemented at commit `901bcd4`; supersedes the v0.1 proposal recorded on 2026-08-04 (deltas in the appendix)

## 1. Problem

Adding a socket DTO to the backend means writing a request, a response, and often an event class by hand, deriving each from `RequestBase`/`ResponseBase`/`EventBase`, and hand-maintaining the `[JsonDerivedType]` registry that drives the polymorphic `$type` dispatch in `Backend/Socket.cs`. The same shapes must then be duplicated by hand as TypeScript interfaces for the frontend, which has no DTO layer at all yet. Every new endpoint is three-plus hand-written files in two languages plus a registry edit. We want one source of truth that generates the C# DTOs at compile time and the TypeScript interfaces, so that adding an endpoint is one small edit to a single file.

## 2. Alternatives considered

| Alternative | Format / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Custom XML contract** (chosen) | In-house DSL; no deps | Attributes map directly onto DTO shape; nested `<Request><Response/>`; flat and clean; full control of generated C# (base classes, `[JsonDerivedType]`) | Ours — we own grammar, parser, emitters | n/a (in-house) | High: matches the shape of the protocol; cheap to build |
| Custom JSON contract | In-house DSL; no deps | Same in-house control; meta-schema gives editor validation + autocomplete | Ours | n/a (in-house) | Medium: better tooling, but the owner prefers XML's syntax and nesting |
| Standard JSON Schema + NJsonSchema | JSON Schema draft 2020-12; NJsonSchema lib | Maintained generators for both C# and TS; runtime validation is free | Very active (RicoSuter), MIT | MIT | Low: "Request/Response/Event" roles don't exist in JSON Schema; output is plain POCOs, not our hierarchy; drags Json.NET into the backend |
| TypeSpec (Microsoft) | DSL; tsp compiler + emitters (Node) | Best authoring (real types, VSCode ext); protocol-agnostic core | Microsoft, very active; client emitters preview | MIT | Low: all relevant client emitters are HTTP-oriented previews — a custom socket emitter is required anyway; adds a Node toolchain to a .NET backend |
| C# as source of truth + NSwag / TS.ContractGenerator | C# classes → TS at build | Keeps C# as the truth; TS in sync via maintained tooling | Active (NSwag) / quiet (TCG) | MIT | Low: does not remove the per-endpoint boilerplate or the registry pain; two-language duplication of intent remains |

Why the others lost: TypeSpec and NJsonSchema both require a custom generator pass anyway because neither can express the `RequestBase`/`ResponseBase`/`EventBase` + `[JsonDerivedType]` + `$type` protocol, and both drag in a second toolchain (Node compiler, or Json.NET) while producing code that is not our house style. Keeping C# as the truth fails the core goal: one source of truth shared with the frontend. Custom JSON offers identical control but the owner explicitly prefers XML's syntax, extensibility, and flat nesting. Between XML and JSON, XML was chosen on taste and fit, not on tooling (JSON's editor validation advantage is acknowledged and discounted because contract files are copy-pasted from neighbors and change rarely).

## 3. Decision & rationale

Build a **custom XML contract format** as the single source of truth for all socket DTOs, with two in-house emitters written in C#:

1. A **C# source generator** (`Backend.Generators.TypesGenerator`, a Roslyn `IIncrementalGenerator`) that reads `types.xml` as an `AdditionalFile` and emits the request/response/event classes, the base classes, and the `[JsonDerivedType]` registrations into `Dto.g.cs` — compile-time, no reflection, no runtime cost.
2. A **standalone CLI** (`Generators/Generator`) exposing the same contract to two emitters in `Generator.Core` — `CsEmitter` and `TsEmitter` — with `-i` input, `-o` output (default stdout), and `-t` target (`Cs` or `Ts`). The TS emitter is written in C#, not in the frontend.

Both emitters parse the same XML through one parser (`Generator.Core.Parser`), so the C# and TS outputs cannot drift from each other.

### Pros

- **One source of truth.** A single XML edit produces the C# classes, the `[JsonDerivedType]` registry, and the TS interfaces. Adding an endpoint stops being a three-file, two-language chore.
- **The backend is generated at compile time.** The source generator makes the DTOs part of the build: deterministic, incremental, zero reflection, no post-build step. A missing or malformed `types.xml` fails the build with a diagnostic, not a runtime surprise.
- **Base classes are generated too.** `DtoBase`/`RequestBase`/`ResponseBase`/`EventBase` come out of the generator, so the hand-maintained registry file is gone entirely.
- **Both emitters share one parser.** The C# and TS output are two renderers over the same model, which is what actually keeps the two languages in sync.
- **The syntax we want.** Flat (`Dto`/`Request`/`Event`/`Property`), attribute-driven, minimal nesting, easy to copy-paste a nearby example. The `<Request><Response/></Request>` nesting encodes "every request has a response", so the pairing cannot be forgotten.
- **Low third-party footprint.** The generator needs two Roslyn packages (`Microsoft.CodeAnalysis.CSharp` 4.14.0, `Microsoft.CodeAnalysis.Analyzers` 3.11.0 — both dev/build-time only) and the CLI needs `CommandLineParser`. Nothing new ships to the backend runtime or the frontend browser.
- **Extensible for validation later.** Grammar can grow (`multiple` already exists; validation attributes can follow) without touching either emitter's contract surface.

### Cons

- **We own the whole language.** Grammar, parser, error messages, documentation, and two emitters are ours to write and maintain.
- **No editor validation.** Without a hand-written XSD (not worth it), typos (`type="Gud"`) surface only at generation time with our error messages (or, for unknown custom types, silently become a `{X}Dto` reference).
- **Not consumable by the ecosystem.** No existing tool reads our format; migrating away later means a translator, not a drop-in.
- **The TS emitter is not wired into the frontend build yet.** Today the frontend gets its interfaces by running the CLI (`-t ts`) and checking the output in (or reading it from stdout). There is no `npm`/build step that regenerates on demand; that handshake is a documented open item.
- **Low authoring-frequency risk is assumed.** The "copy-paste, rare edits" argument holds only if DTOs genuinely stabilize; churn in the early game-design phase will be paying down DSL friction.
- **No optionality yet.** Every property is required; optional properties would need a grammar change (deferred).

## 4. Build-vs-buy

Implemented effort matched the estimate: **~1–3 days** for the source generator (parse `AdditionalFile` XML, emit classes + `DtoBase` registry) and **~0.5–1 day** for the TS emitter, both delivered in this branch. The parser lives once in `Generator.Core` and is shared, so the TS emitter cost was lower than the original two-emitter estimate suggested.

Buying doesn't remove the work: TypeSpec's client emitters are HTTP-oriented previews, so a custom socket emitter (≈ the same effort as our generator, on top of their toolchain) is unavoidable, and NJsonSchema cannot emit our base-class hierarchy and drags in Json.NET while producing non-house-style POCOs. Both "buy" options would still leave us writing a generator pass — buying would only replace our effort with their integration friction and a bigger dependency footprint. Building wins.

## 5. Risk

### Undo risk — medium

Confined to the DTO layer and the socket contract. Reverting to hand-written C# DTOs is trivial (the generated output is comparable to what we'd write by hand, and `EmitCompilerGeneratedFiles` keeps it reviewable), and the TS emitter can be discarded. But the XML is now the source of truth for the protocol and a change of format later means writing a translator for every existing contract.

### Security risk — low

No backend runtime dependency from the generator: it runs in-process at build time and touches only the committed contract file via `AdditionalFiles` (no arbitrary IO). The generator's Roslyn packages are `PrivateAssets="all"` and never shipped; the CLI is a dev tool. The generator must not be made to follow external XML sources; contract files are committed, reviewed inputs. (See [Microsoft.CodeAnalysis.CSharp](./microsoft-codeanalysis-csharp.md) and [Microsoft.CodeAnalysis.Analyzers](./microsoft-codeanalysis-analyzers.md) for the package-level risk.)

## Appendix: implemented format (v0.2, as-shipped)

The contract file is `types.xml` at the solution root. The current contract (abridged from the repo):

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

  <Event name="ProfilesChanged">
    <Property name="Profiles" type="Profile" multiple="true" />
  </Event>

  <Request name="ListProfiles">
    <Response>
      <Property name="Profiles" type="Profile" multiple="true" />
    </Response>
  </Request>

  <Response name="Error">
    <Property name="Message" type="string" />
  </Response>
</Types>
```

Semantics:

- Root element is `<Types>`. All message elements are its direct children, in any order.
- Element kind selects the generated base class: `Dto` → `DtoBase`, `Request` → `RequestBase`, `Response` → `ResponseBase`, `Event` → `EventBase`.
- Generated names follow `{Name}Dto` / `{Name}Request` / `{Name}Response` / `{Name}Event`; the source `name` carries **no** suffix.
- A `Request` must contain exactly one child `<Response>` (the parser errors otherwise). The response's generated name defaults to `{RequestName}Response`, overridable via a `name` attribute on the `<Response>` element.
- `Property` attributes:
  - `name` — required; cased to `UpperCamelCase` for the class member and `lowerCamelCase` for the JSON/TS key.
  - `type` — required. Tokens: `string`, `int`, `float`, `Guid`, or the bare name of another declared contract type — a `<Dto>` (which becomes `{X}Dto`, e.g. `Profile` → `ProfileDto`) or an `<Enum>` (which becomes `{X}` as-is, e.g. `ItemId`). An unrecognized token is a parse error, not a silent `{X}Dto` reference.
  - `multiple="true"` — optional; makes the member an array (`string[]` in C#, `type[]` in TS).
- Every property is required; there is no optionality or validation syntax yet.

Generated C# (`Dto.g.cs`, emitted by `TypesGenerator`):

- `// <auto-generated/>`, `namespace Backend.Dtos;`, `public sealed class {Name} : {Base}` with `[JsonPropertyName("...")] public {Type} {Member} { get; init; }` per property.
- The base classes are generated in the same file: `DtoBase`, `RequestBase : DtoBase` and `ResponseBase : DtoBase` (each with `int RequestId { get; set; }`), `EventBase : DtoBase` (with `int EventId`).
- `[JsonPolymorphic]` plus one `[JsonDerivedType(typeof({Name}), nameof({Name}))]` per generated type annotates `DtoBase` — the registry the old hand-written `DtoBase.cs` carried.
- `public static class DropTableData` with `public static void AddAll(DropTableService service)` (the file adds `using Backend.Services;`): seeds every declared `<DropTable>` into a `Backend.Services.DropTableService`, emitting `item=` drops as `ItemDrop`/`ItemId` and `table=` drops as `TableDrop`/`DropTableId`.

Generated TypeScript (via `dotnet run --project Generators/Generator -- -i types.xml -t ts`):

- `interface DtoBase { $type: string; }`; `RequestBase`/`ResponseBase` each with `requestId: number;`; `EventBase` with `eventId: number;`; then per object `interface {Name} extends {Base}` with `{key}: {type};` per property.

Wiring:

- `Backend/Backend.csproj` references `Generator.Backend` and `Generator.Core` as analyzers (`OutputItemType="Analyzer" ReferenceOutputAssembly="false"`) and adds `<AdditionalFiles Include="..\types.xml" />`. The `Generator.Core` reference is **load-bearing, not redundant**: Roslyn only loads a source generator's dependencies if they are themselves registered as analyzers (dotnet/roslyn discussion #47517), so dropping it fails the build with a `FileNotFoundException` on `Generator.Core` even though `Generator.Core.dll` sits beside `Generator.Backend.dll`.
- Errors: **DTC001** (no file named `types.xml` among `AdditionalFiles`) and **DTC002** (parse failure), both `Error` severity so a broken contract breaks the build.
- The CLI is `Generators/Generator` (net10.0 exe, [CommandLineParser](./commandlineparser.md)): `-i` input (required), `-o` output (default stdout), `-t` target `Cs|Ts` (required). Generated code goes to stdout/a file; every log line goes to stderr.

Deltas from v0.1 (2026-08-04):

- Root renamed `<DtoContract xmlns=...>` → `<Types>`, no namespace.
- `Dto name` no longer carries the `Dto` suffix in the source.
- `required="true"` dropped — all properties are required; arrays moved from `T[]` tokens to `multiple="true"`.
- Base classes and the `[JsonDerivedType]` registry are now generated rather than hand-maintained.
- The TS emitter is a C# emitter in the CLI, not `fast-xml-parser` in the frontend — see the library index.

Open design points (deferred, deliberately):

- **Validation syntax** — not designed; `Validation="..."` is a placeholder idea, not a commitment.
- **Optional properties** — every property is currently required; adding `required="false"` (or similar) is a grammar change.
- **XSD / editor support** — not building an XSD; rely on copy-paste patterns.
- **Frontend handshake** — *resolved 2026-08-30*: the generated TS reaches `Frontend/` through a wired build step, not a checked-in file. Locally, `Frontend/package.json` runs the CLI before `dev`, `build` and `check`; in the Docker build, `Frontend/Dockerfile` regenerates the schema in a .NET SDK stage from the same commit's `types.xml` (see [deployment](../deployment.md)). Generated TS files use the `*.generated.ts` pattern (e.g., `dto.generated.ts`), which the root `.gitignore` rule `**/*.generated.*` excludes at any depth — including under `Frontend/` — so the output is regenerated on demand rather than committed.
- Whether the source generator also emits the socket endpoint wiring (currently reflection in `Backend/Services/SocketEndpointService.cs`) — separate follow-up.
