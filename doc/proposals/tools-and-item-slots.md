# Proposal — tools and item slots

- Status: **implemented (parsing/seeding); gameplay wiring pending**
- Date: 2026-08-27
- Scope: defines how tools, item slots, and per-item stats are declared in `types.xml`

> **Implementation status:** the parser, model, and emitters in the generator project now support `<Item>` (with ordered tags + stats) and `<Skill>` (with nested item slots), and a `ToolData` seeder + `Backend.Services.ToolService` populate the runtime. What is **not** done yet is wiring the stats into actual gameplay (activity speed, drop rolls, XP, durability). See [Implementation notes](#7-implementation-notes).

## 1. Problem

Skills today are trained by running activities, and activities take a fixed amount of groundwork. There is no notion of *equipment* or *tools*: a player cannot socket a better pickaxe head and mine faster, drop more items, or earn more XP. We want a data-driven way to:

- give each skill a set of **item slots** (e.g. Mining → `Handle` + `Head`, which together form a full pickaxe),
- declare which **items are valid** in each slot (e.g. `IronPickaxeHead` fits the `MiningHead` slot),
- give each item **stats** that modify how the skill plays.

All of this is content data and should be declared declaratively in `types.xml` alongside the existing `Enum` / `DropTable` / `Activity` declarations, so that designer-led balance changes are a content edit, not a code change.

## 2. Proposed XML shape

### 2.1 New contract elements

Two new elements (plus the standalone `<Skill>`) are direct children of `<Types>` in `types.xml`:

1. **`<Item>`** — declares a concrete item, its **tags**, and its **stats**. Tags are ordered: the first tag is the item's *main* tag, the second the *secondary*, and so on.
2. **`<Skill>`** — declares a skill (auto-registers into `SkillId`) and, nested inside it, the skill's **item slots**. Each `<Slot>` carries a `required` flag and a single nested `<Tag>`; slots auto-register into `ItemSlotId`. There is no standalone `<SkillSlots>` or `<ItemSlot>` element — a skill's slots live directly inside its `<Skill>` tag.

Every item tag auto-registers into an `ItemTagId` enum (deduplicated). A slot accepts any item whose tags include the slot's `<Tag>`.

### 2.2 Item stats

The set of supported stats (each numeric, higher is better unless noted):

| Stat | Meaning |
|---|---|
| `speed` | Multiplier on how fast an activity completes (higher = faster). |
| `itemProductivity` | How many item drop tables are rolled per action completion (higher = more drops). |
| `xpProductivity` | XP multiplier applied to the activity's XP reward (higher = more XP). |
| `durable` | Multiplier making the item lose durability slower (higher = lasts longer). |

### 2.3 Example declaration

```xml
<Skill name="Mining">
  <Slot name="Head" required="true">
    <Tag name="head" />
  </Slot>
  <Slot name="Handle" required="true">
    <Tag name="handle" />
  </Slot>
</Skill>
<Skill name="LumberJacking" />
<Skill name="Crafting" />

<Item name="IronPickaxeHead">
  <Tag name="head" />   <!-- main tag -->
  <Tag name="iron" />   <!-- secondary tag -->
  <Stat name="speed" value="1.1" />
  <Stat name="itemProductivity" value="1.25" />
  <Stat name="xpProductivity" value="1.0" />
  <Stat name="durable" value="1.5" />
</Item>

<Item name="OakHandle">
  <Tag name="handle" />
  <Stat name="speed" value="0.9" />
  <Stat name="durable" value="1.2" />
</Item>
```

Here the `Head` slot accepts any item tagged `head` (so `IronPickaxeHead`), and the `Handle` slot accepts any item tagged `handle` (so `OakHandle`).

## 3. Design notes & open questions

- **Full-tool composition:** slotting all `required="true"` slots is what forms the "full" tool (a complete pickaxe). Whether a partial tool still grants its individual slots' stats is an open question — probably individual-item stats apply as equipped, and only a full set unlocks a set bonus (not yet defined).
- **Where stats aggregate:** the obvious semantics are that equipped items' stats multiply together (a head ×1.1 speed and a handle ×0.9 speed = ×0.99 combined). Sum-vs-multiply for stacking is an open decision.
- **Naming/casing:** following the existing contract, `name` values are bare PascalCase words (no suffix); emitters would append suffixes if we generate DTOs/enums from these declarations (`MiningHeadSlot`, `IronPickaxeHeadItem`, etc.). Tag names are lower-case words (`head`, `iron`, `handle`).
- **Enum generation:** like `DropTable`/`Activity` auto-register `DropTableId`/`ActivityId` enums, `Item` auto-registers into `ItemId`, `Skill` into `SkillId`, item tags into `ItemTagId`, and slots into `ItemSlotId`.
- **Parser integration:** the `Parser.Elements` dispatch in `Generators/Core/Parser.cs` handles `case "Item"` and `case "Skill"`. A `<Skill>` parses its nested `<Slot>` children directly. Unknown top-level elements are currently a parse error, so these were added for the elements to parse.
- **Wire-format exposure:** to include equipped tools in a socket request/response, add a DTO (e.g. `<Dto name="EquippedTool">` with a slot→item mapping) — that is a follow-up, not part of this data-shape proposal.
- **Item base stats:** whether the stat values here are the *total* or stacked on a base (e.g. a default ×1.0 pickaxe) is open. The example uses absolute multipliers.

## 4. Alternative considered

| Alternative | Description | Verdict |
|---|---|---|
| **Declarative XML in types.xml** (chosen) | Stats/slots as content in the single source of truth, generated like `DropTable`/`Activity` | High fit — matches existing content-pipeline; one file keeps game data together |
| Hard-coded C# / database | Tools and slots baked into services or an EF table | Rejected — splits game data across code/DB and contradicts the declarative `types.xml` direction |

## 5. Open items

- Aggregate semantics for multiple slots (sum vs. multiply) and set bonuses for full tools.
- Whether `speed`/`durable` apply per-item as equipped or only meaningfully for complete tools (e.g. `durable` needs durability — does the current game have item durability?).
- Whether the accepted tag on a slot is better modelled as a set of tags (match any) rather than a single tag.
- Enum / DTO integration for exposing equipped tools over the socket.
- How the frontend learns which items are valid for which slot (do item tags expose this directly, or does it need a generated DTO?).

## 6. Related documents

- [DTO contract](../backend/dto-contract.md) — how `types.xml` elements drive generated C#/TS.
- [Socket endpoints](../backend/socket-endpoints.md) — where equipped-tool data would be queried/updated.

## 7. Implementation notes

The generator-side parsing is implemented as of 2026-08-27:

- **Parser (`Generators/Core/Parser.cs`)** recognizes `Item` and `Skill` elements. Item stats are validated against the known set (`speed`, `itemProductivity`, `xpProductivity`, `durable`); an unknown stat is a parse error. Items auto-populate the `ItemId` enum and `Skill`s the `SkillId` enum; each `<Item>` `<Tag>` deduplicated into an `ItemTagId` enum; each nested `<Slot>` registers an `ItemSlotId` value. Since `Item`/`Skill` are now content tags, the hand-written `ItemId`/`SkillId` `<Enum>` declarations were removed from `types.xml`; the `<Enum>` element remains supported for arbitrary enums.
- **Model (`Generators/Core/DtoModel.cs`)** gains `Item` (with `ItemTag`/`ItemStat`) and `Skill` (with nested `Slot`, each carrying its accepted `ItemTag`) types; the earlier standalone `ItemSlot`/`ValidItem` types were removed.
- **C# emitter (`Generators/Core/CsEmitter.cs`)** emits a `ToolData.AddAll(ToolService)` seeder that populates `ItemDefinition(tags, stats)` and, for each skill that declares slots, `AddSkillSlots(SkillId.X, [SlotBinding(slotId, tag, required)...])`. It also exposed a latent culture bug: numeric literal parsing (`RequireAttribute<T>`) used the ambient culture, so decimal `value`s misparsed on non-en-US hosts (e.g. `en-DK` turned `1.1` into `11`). Fixed to parse with `CultureInfo.InvariantCulture` in `XmlElementExtensions.ConvertValue` — this affects all numeric attributes, not just the new ones.
- **Backend (`Backend/Services/ToolService.cs`)** holds the runtime definitions (`ItemDefinition` with ordered tags, `ItemStat`, `SlotBinding` with its accepted tag, `SkillSlotDefinition`) and resolves valid items by tag via `GetValidItems(ItemTagId)`. It is registered in DI and seeded from `ToolData.AddAll` in `AppHost.cs`.

Still open (not implemented): applying stats to gameplay (activity duration from `speed`, extra drop-table rolls from `itemProductivity`, XP multiplier from `xpProductivity`, durability consumption from `durable`), equipping/unequipping without a socket/ToolService consumer, and any aggregate/set-bonus semantics.
