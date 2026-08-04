# DTO XML contract

- Status: in-house
- Date: 2026-08-04
- Decided by: project owner
- Version / commit pinned: n/a — format proposal v0.1; no code committed yet

## 1. Problem

Adding a socket DTO to the backend means writing a request, a response, and often an event class by hand (`Backend/Dtos/Auth/*`), deriving each from `RequestBase`/`ResponseBase`/`EventBase`, and hand-maintaining the `[JsonDerivedType]` registry in `Backend/Dtos/DtoBase.cs` that drives the polymorphic `$type` dispatch in `Backend/Socket.cs`. The same shapes must then be duplicated by hand as TypeScript interfaces for the frontend, which has no DTO layer at all yet. Every new endpoint is three-plus hand-written files in two languages plus a registry edit. We want one source of truth that generates the C# DTOs at compile time and the TypeScript interfaces for the frontend, so that adding an endpoint is one small edit to a single file.

## 2. Alternatives considered

| Alternative | Format / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Custom XML contract** (chosen) | In-house DSL; no deps | Attributes map directly onto DTO shape; nested `<Request><Response/>`; flat and clean; full control of generated C# (base classes, `[JsonDerivedType]`, `required`) | Ours — we own grammar, parser, emitters | n/a (in-house) | High: matches the shape of the protocol; cheap to build |
| Custom JSON contract | In-house DSL; no deps | Same in-house control; meta-schema gives editor validation + autocomplete | Ours | n/a (in-house) | Medium: better tooling, but the user prefers XML's syntax and nesting |
| Standard JSON Schema + NJsonSchema | JSON Schema draft 2020-12; NJsonSchema lib | Maintained generators for both C# and TS; runtime validation is free | Very active (RicoSuter), MIT | MIT | Low: "Request/Response/Event" roles don't exist in JSON Schema; output is plain POCOs, not our hierarchy; drags Json.NET into the backend |
| TypeSpec (Microsoft) | DSL; tsp compiler + emitters (Node) | Best authoring (real types, VSCode ext); protocol-agnostic core | Microsoft, very active; client emitters preview | MIT | Low: all relevant client emitters are HTTP-oriented previews — a custom socket emitter is required anyway; adds a Node toolchain to a .NET backend |
| C# as source of truth + NSwag / TS.ContractGenerator | C# classes → TS at build | Keeps C# as the truth; TS in sync via maintained tooling | Active (NSwag) / quiet (TCG) | MIT | Low: does not remove the per-endpoint boilerplate or the registry pain; two-language duplication of intent remains |

Why the others lost: TypeSpec and NJsonSchema both require a custom generator pass anyway because neither can express the `RequestBase`/`ResponseBase`/`EventBase` + `[JsonDerivedType]` + `$type` protocol, and both drag in a second toolchain (Node compiler, or Json.NET) while producing code that is not our house style. Keeping C# as the truth fails the core goal: one source of truth shared with the frontend. Custom JSON offers identical control but the owner explicitly prefers XML's syntax, extensibility, and flat nesting. Between XML and JSON, XML was chosen on taste and fit, not on tooling (JSON's editor validation advantage is acknowledged and discounted because contract files are copy-pasted from neighbors and change rarely).

## 3. Decision & rationale

Build a **custom XML contract format** as the single source of truth for all socket DTOs, with two in-house emitters:

1. A **C# source generator** (Roslyn `ISourceGenerator`) that reads the XML as an `AdditionalFile` and emits the request/response/event classes (derived from the existing base classes) plus the `[JsonDerivedType]` registrations into a partial `DtoBase` — compile-time, no reflection, no runtime cost.
2. A **TypeScript emitter** in the frontend that parses the same XML (via `fast-xml-parser`) and emits interfaces + `$type` literal unions + a discriminated union for the socket envelope.

### Pros

- **One source of truth.** A single XML edit produces the C# classes, the `[JsonDerivedType]` registry, and the TS interfaces. Adding an endpoint stops being a three-file, two-language chore.
- **Byte-perfect generated C#.** Because we write the emitter, the output is exactly house style: `sealed partial` classes, `required` properties, correct base classes, correct discriminator names — the `DtoBase` registry is generated, not hand-edited.
- **Compile-time generation.** The C# side is a source generator: deterministic, incremental, zero reflection, no post-build step.
- **The syntax we want.** Flat (`Dto`/`Request`/`Event`/`Property`), attribute-driven, minimal nesting, easy to copy-paste a nearby example. The `<Request><Response/></Request>` nesting encodes "every request has a response", so the pairing cannot be forgotten.
- **Extensible for validation later.** Grammar can grow (`Validation="NotNull,ValidUser"`) without touching either emitter's contract surface.
- **Low third-party footprint.** Backend adds no new dependency; the frontend adds one small pure-JS parser.

### Cons

- **We own the whole language.** Grammar, parser, error messages, documentation, and two emitters are ours to write and maintain.
- **No editor validation.** Without a hand-written XSD (not worth it), typos (`type="Gud"`) surface only at generation time with our error messages.
- **Not consumable by the ecosystem.** No existing tool reads our format; migrating away later means a translator, not a drop-in.
- **A frontend dependency.** `fast-xml-parser` is required for the TS emitter — small (MIT, pure JS, ~29 KB) but a new package needing its own decision doc.
- **Low authoring-frequency risk is assumed.** The "copy-paste, rare edits" argument holds only if DTOs genuinely stabilize; churn in the early game-design phase will be paying down DSL friction.

## 4. Build-vs-buy

In-house effort: **~1–3 days** for the C# source generator (parse `AdditionalFile` XML, emit classes + `DtoBase` registry), **~0.5–1 day** for the TS emitter (interfaces are flat value types). Total **~2–4 days**.

Buying doesn't remove the work: TypeSpec's client emitters are HTTP-oriented previews, so a custom socket emitter (≈ the same effort as our generator, on top of their toolchain) is unavoidable, and NJsonSchema cannot emit our base-class hierarchy and drags in Json.NET while producing non-house-style POCOs. Both "buy" options would still leave us writing a generator pass — buying would only replace our ~2–4 days with their integration friction and a bigger dependency footprint. Building wins.

## 5. Risk

### Undo risk — medium

Confined to the DTO layer and the socket contract. Reverting to hand-written C# DTOs is trivial (the generated output is comparable to what we'd write by hand, and `EmitCompilerGeneratedFiles` keeps it reviewable), and the TS emitter can be discarded. But once the XML is the source of truth, the format is load-bearing for the protocol and a change of format later means writing a translator for every existing contract.

### Security risk — low

No new backend runtime dependency: the source generator runs in-process at build time and touches only the trusted contract files via `AdditionalFiles` (no arbitrary IO). The only new dependency is `fast-xml-parser` in the frontend — MIT, pure JS, no native binaries, actively maintained. The generator must not be made to follow external XML sources; contract files are committed, reviewed inputs.

## Appendix: format proposal (v0.1)

Representative contract:

```xml
<DtoContract xmlns="https://openidle.example/dto-contract">

  <Dto name="ProfileDto">
    <Property name="Name" type="string" required="true" />
    <Property name="ProfileId" type="Guid" required="true" />
  </Dto>

  <Request name="ListProfiles">
    <Response>
      <Property name="Profiles" type="ProfileDto[]" />
    </Response>
  </Request>

  <Request name="SelectProfile">
    <Property name="ProfileId" type="Guid" />
    <Response />
  </Request>

  <Request name="Ping">
    <Response name="Pong" />
  </Request>

  <Event name="ProfilesChanged">
    <Property name="Profiles" type="ProfileDto[]" />
  </Event>

</DtoContract>
```

Semantics:

- Element kind selects the base class: `Request` → `RequestBase`, `Response` → `ResponseBase`, `Event` → `EventBase`, `Dto` → plain class.
- Generated names follow `{Name}Request`/`{Name}Response`/`{Name}Event`; the default response name is the request name + `Response`, overridable via `name` (e.g. `Ping` → `Pong`).
- `Property` `type` tokens start as: `string`, `Guid`, `int`/`int32`, `long`/`int64`, `double`, `bool`, `DateTime`, `T[]`. Optional via absence of `required="true"`.
- The generator emits `[JsonDerivedType]` for every generated message into a partial `DtoBase`, replacing the hand-maintained registry at `Backend/Dtos/DtoBase.cs:6-17`.

Open design points (deferred, deliberately):

- **Validation syntax** — explicitly not designed now; `Validation="..."` on `Property` is a placeholder, not a commitment.
- **XSD / editor support** — not building an XSD; rely on copy-paste patterns.
- **TS emitter details** — `fast-xml-parser` adoption, `$type` union shape, and where generated files land are frontend decisions; document separately.
- Whether the source generator also emits the `SocketEndpointService` registration (currently reflection at `Backend/Services/SocketEndpointService.cs:82-105`) — separate follow-up.
