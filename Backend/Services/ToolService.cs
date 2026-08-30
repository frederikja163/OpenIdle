using System;
using System.Collections.Generic;
using Backend.Dtos;

namespace Backend.Services;

/// <summary>
/// The stats an item may carry on its <c>ItemStat</c>. Keep the numeric values in lockstep with
/// <c>Generator.Core.ItemStats.ByKey</c> (the generator's <c>ToolData</c> seeder maps an item's stat names
/// to these enum members) — an item declared with a stat whose <c>ToolStat</c> member is missing here
/// will fail to compile the generated <c>ToolData.AddAll</c>.
/// </summary>
public enum ToolStat
{
    Speed,
    ItemProductivity,
    XpProductivity,
    Durable,
}

public sealed class ItemStat(ToolStat stat, float value)
{
    public ToolStat Stat { get; } = stat;
    public float Value { get; } = value;
}

public sealed class ItemDefinition(ItemTagId[] tags, ItemStat[] stats)
{
    /// <summary>Ordered item tags. The first is the item's main tag, the second the secondary, and so on.</summary>
    public IReadOnlyList<ItemTagId> Tags { get; } = tags;
    public IReadOnlyList<ItemStat> Stats { get; } = stats;
}

public sealed class SlotBinding(ItemSlotId slotId, ItemTagId tag, bool required)
{
    public ItemSlotId SlotId { get; } = slotId;

    /// <summary>The tag an item must carry to be valid in this slot.</summary>
    public ItemTagId Tag { get; } = tag;
    public bool Required { get; } = required;
}

public sealed class SkillSlotDefinition(SkillId skillId, IReadOnlyList<SlotBinding> slots)
{
    public SkillId SkillId { get; } = skillId;
    public IReadOnlyList<SlotBinding> Slots { get; } = slots;
}

public sealed class ToolService
{
    private readonly Dictionary<ItemId, ItemDefinition> _items = new();
    private readonly Dictionary<ItemTagId, List<ItemId>> _itemsByTag = new();
    private readonly Dictionary<SkillId, SkillSlotDefinition> _skillSlots = new();

    public void AddItem(ItemId itemId, ItemDefinition definition)
    {
        _items.Add(itemId, definition);
        foreach (ItemTagId tag in definition.Tags)
        {
            if (!_itemsByTag.TryGetValue(tag, out List<ItemId>? itemIds))
            {
                itemIds = [];
                _itemsByTag[tag] = itemIds;
            }

            itemIds.Add(itemId);
        }
    }

    public void AddSkillSlots(SkillId skillId, IReadOnlyList<SlotBinding> slots)
    {
        _skillSlots.Add(skillId, new SkillSlotDefinition(skillId, slots));
    }

    public bool TryGetItem(ItemId itemId, out ItemDefinition? definition)
    {
        return _items.TryGetValue(itemId, out definition);
    }

    /// <summary>Returns the ids of every item that carries the given tag.</summary>
    public ItemId[] GetValidItems(ItemTagId tag)
    {
        return _itemsByTag.TryGetValue(tag, out List<ItemId>? itemIds) ? itemIds.ToArray() : [];
    }

    public bool TryGetSkillSlots(SkillId skillId, out IReadOnlyList<SlotBinding>? slots)
    {
        if (_skillSlots.TryGetValue(skillId, out SkillSlotDefinition? definition))
        {
            slots = definition.Slots;
            return true;
        }

        slots = null;
        return false;
    }

    public IReadOnlyList<SkillSlotDefinition> GetSkillSlots()
    {
        return [.. _skillSlots.Values];
    }
}
