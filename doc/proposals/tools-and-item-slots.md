# Proposal — tools and item slots

- Status: **proposal** (not implemented)
- Date: 2026-08-27
- Scope: defines how tools, item slots, and per-item stats are declared in `types.xml`

## 1. Problem

Skills today are trained by running activities, and activities take a fixed amount of groundwork. There is no notion of *equipment* or *tools*: a player cannot socket a better pickaxe head and mine faster, drop more items, or earn more XP. We want a data-driven way to:

- give each skill a set of **item slots** (e.g. Mining → `Handle` + `Head`, which together form a full pickaxe),
- declare which **items are valid** in each slot (e.g. `IronPickaxeHead` fits the `MiningHead` slot),
- give each item **stats** that modify how the skill plays.

All of this is content data and should be declared declaratively in `types.xml` alongside the existing `Enum` / `DropTable` / `Activity` declarations, so that designer-led balance changes are a content edit, not a code change.

## 2. Proposed XML shape

### 2.1 New contract elements

Three new elements are proposed, all direct children of `<Types>` in `types.xml`:

1. **`<Item>`** — declares a concrete item and its stats.
2. **`<ItemSlot>`** — declares a slot a skill can equip, plus the set of items valid in it.
3. **`<SkillSlots>`** (or an attribute on `<Skill>`) — binds slots to a skill, including whether all slots are required to form a "full" tool.

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
<Item name="IronPickaxeHead">
  <Stat name="speed" value="1.1" />
  <Stat name="itemProductivity" value="1.25" />
  <Stat name="xpProductivity" value="1.0" />
  <Stat name="durable" value="1.5" />
</Item>

<Item name="OakHandle">
  <Stat name="speed" value="0.9" />
  <Stat name="durable" value="1.2" />
</Item>

<ItemSlot name="MiningHead">
  <ValidItem name="IronPickaxeHead" />
</ItemSlot>

<ItemSlot name="Handle">
  <ValidItem name="OakHandle" />
</ItemSlot>

<SkillSlots skill="Mining">
  <Slot name="MiningHead" required="true" />
  <Slot name="Handle" required="true" />
</SkillSlots>
```

## 3. Design notes & open questions

- **Full-tool composition:** slotting all `required="true"` slots is what forms the "full" tool (a complete pickaxe). Whether a partial tool still grants its individual slots' stats is an open question — probably individual-item stats apply as equipped, and only a full set unlocks a set bonus (not yet defined).
- **Where stats aggregate:** the obvious semantics are that equipped items' stats multiply together (a head ×1.1 speed and a handle ×0.9 speed = ×0.99 combined). Sum-vs-multiply for stacking is an open decision.
- **Naming/casing:** following the existing contract, `name` values are bare PascalCase words (no suffix); emitters would append suffixes if we generate DTOs/enums from these declarations (`MiningHeadSlot`, `IronPickaxeHeadItem`, etc.).
- **Enum generation:** like `DropTable`/`Activity` auto-register `DropTableId`/`ActivityId` enums, `Item` could auto-extend the existing `ItemId` enum, and `ItemSlot`/`SkillSlots` could generate `ItemSlotId`/`ToolId` enums for use in the socket protocol.
- **Parser integration:** the `Parser.Elements` dispatch in `Generators/Core/Parser.cs:36-74` would gain `case "Item"`, `case "ItemSlot"`, `case "SkillSlots"`. Unknown top-level elements are currently a parse error in `Parser.cs:72` (unlike the DTO comment claiming they're skipped), so these must be added or the file won't parse.
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
- Exact element/attribute naming — `SkillSlots` vs. slots nested under a new `<Skill>` element.
- Enum / DTO integration for exposing equipped tools over the socket.
- How the frontend learns which items are valid for which slot (does it need the valid-item set as a generated DTO, or just in the backend validation?).

## 6. Related documents

- [DTO contract](../backend/dto-contract.md) — how `types.xml` elements drive generated C#/TS.
- [Socket endpoints](../backend/socket-endpoints.md) — where equipped-tool data would be queried/updated.
