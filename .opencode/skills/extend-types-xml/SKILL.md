---
name: extend-types-xml
description: Use when extending the types.xml contract grammar itself - adding a new attribute or new element tag to the DTO XML format, or a new built-in property type. Trigger words include "add an attribute to types.xml", "new tag in types.xml", "new property type", "update the parser", "update the emitter", "generator change". This changes the generator pipeline (Generators/Core), not just declarations inside types.xml - for simply declaring new requests/dtos/enums in existing syntax use the add-endpoint skill instead.
---

# Extending the types.xml contract grammar

This is a generator-pipeline change: the XML contract gains a new attribute, element, or built-in property type, and the shared parser + emitters must learn it. The pipeline is one parser feeding two emitters:

```text
types.xml → Generators/Core/Parser.cs → DtoModel → CsEmitter (build time) / TsEmitter (CLI)
```

## Step 0 — read the docs first

Per the `consult-docs` skill, read `doc/libraries/dto-xml-contract.md` and `doc/backend/dto-contract.md` in full before editing, then confirm against source — both documents have drifted from code in places (e.g. they omit `optional="true"` and claim unknown elements are ignored; `Generators/Core/Parser.cs` actually throws on them).

## Step 1 — add the attribute/tag to types.xml

Edit [`types.xml`](../../../types.xml) and use the new syntax on an appropriate element. Copy the closest existing pattern (`<Property>` attributes, `<Enum>/<Value>`, `<DropTable>`, `<Activity>`, reward elements). Keep names simple PascalCase words — they pass through the casing splitter (`Generators/Core/StringCasing.cs`).

## Step 2 — update the parser

Two files, both in `Generators/Core/`:

- [`DtoModel.cs`](../../../Generators/Core/DtoModel.cs) — the parsed model. Add/extend model classes and `DtoModel` collections here. New **built-in** property types go into the `PropertyType` enum.
- [`Parser.cs`](../../../Generators/Core/Parser.cs) — the reader:
  - New top-level element → add a case to the `Element()` switch (unknown elements throw).
  - New child element → add a case in the owning element's parse method (see how `Activity()` switches over its children).
  - New attribute → read it with `element.RequireAttribute("...")` / `element.GetAttribute<T>("...", default)` (helpers in `Generators/Core/Extensions/XmlElementExtensions.cs`) and carry it on the model class.

Follow the existing error style: throw `ParserException` with a message naming the offending element/property. A malformed contract must fail loudly (DTC002 in the backend build).

## Step 3 — update the C# emitter

[`CsEmitter.cs`](../../../Generators/Core/CsEmitter.cs):

- New built-in property type → add the mapping in `GetPropertyType`.
- New element kind that generates classes/interfaces → it flows through `EmitDtos` via `model.AllObjects`; add its base-class mapping in `BaseType` if needed.
- New element kind that seeds backend data → follow `EmitDropTableData` / `EmitActivityData` and emit calls into the corresponding hand-written service.

The C# output must compile against whatever hand-written backend types you reference (e.g. `ItemReward`, `ActivityDefinition` in `Backend/Services/`) — extend those first if the emitted expressions need new constructors.

## Step 4 — TypeScript emitter for shared DTO changes

[`TsEmitter.cs`](../../../Generators/Core/TsEmitter.cs) must be updated whenever a change adds a new `PropertyType` enum member or adds a new object to `DtoModel.AllObjects` (i.e., new Dto/Request/Response/Event shape) — update `GetPropertyType` and `EmitDtos` as needed so the TS and C# emitters stay in sync. Backend-only additions like `DropTable`/`Activity` elements remain excluded from this requirement since they're not part of `AllObjects` and have no TS representation. If you deliberately skip TypeScript for a backend-only addition, state so in your summary.

## Step 5 — verify

```powershell
# Parser + C# emitter: build proves the contract parses and generated code compiles
dotnet build Backend\Backend.csproj

# Inspect the generated C#
dotnet run --project Generators\Generator -- -i types.xml -t Cs

# Only if TS support was requested
dotnet run --project Generators\Generator -- -i types.xml -t Ts
```

Then exercise the new grammar end-to-end: declare something using it in `types.xml`, rebuild, and confirm the expected output appears (DTC002 with your `ParserException` message means the parser rejected it).
