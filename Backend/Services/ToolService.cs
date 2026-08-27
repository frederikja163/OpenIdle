using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Backend.Dtos;

namespace Backend.Services;

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

public sealed class ItemDefinition(params ItemStat[] stats)
{
    public IReadOnlyList<ItemStat> Stats { get; } = stats;
}

public sealed class SlotBinding(ItemSlotId slotId, bool required)
{
    public ItemSlotId SlotId { get; } = slotId;
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
    private readonly Dictionary<ItemSlotId, IReadOnlyList<ItemId>> _slotValidItems = new();
    private readonly List<SkillSlotDefinition> _skillSlots = new();

    public void AddItem(ItemId itemId, ItemDefinition definition)
    {
        _items.Add(itemId, definition);
    }

    public void AddItemSlot(ItemSlotId itemSlotId, IReadOnlyList<ItemId> validItems)
    {
        _slotValidItems.Add(itemSlotId, validItems);
    }

    public void AddSkillSlots(SkillId skillId, IReadOnlyList<SlotBinding> slots)
    {
        _skillSlots.Add(new SkillSlotDefinition(skillId, slots));
    }

    public bool TryGetItem(ItemId itemId, out ItemDefinition? definition)
    {
        return _items.TryGetValue(itemId, out definition);
    }

    public bool TryGetValidItems(ItemSlotId itemSlotId, out IReadOnlyList<ItemId>? validItems)
    {
        return _slotValidItems.TryGetValue(itemSlotId, out validItems);
    }

    public bool TryGetSkillSlots(SkillId skillId, out IReadOnlyList<SlotBinding>? slots)
    {
        SkillSlotDefinition? definition = _skillSlots.FirstOrDefault(s => s.SkillId == skillId);
        slots = definition?.Slots;
        return definition is not null;
    }

    public IReadOnlyList<SkillSlotDefinition> GetSkillSlots()
    {
        return new ReadOnlyCollection<SkillSlotDefinition>(_skillSlots);
    }
}
