using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Backend.Dtos;

namespace Backend.Services;

public abstract class Reward(int count, float? weight)
{
    public int Count { get; } = count;
    public float? Weight { get; } = weight;

    protected abstract string DisplayId { get; }

    public override string ToString()
    {
        return $"[{Weight}|{Count}x{DisplayId}]";
    }
}

public sealed class TableReward(int count, float? weight, DropTableId dropTableId) : Reward(count, weight)
{
    public DropTableId DropTableId { get; } = dropTableId;

    protected override string DisplayId => DropTableId.ToString();
}

public sealed class ItemReward(int count, float? weight, ItemId itemId) : Reward(count, weight)
{
    public ItemId ItemId { get; } = itemId;

    protected override string DisplayId => ItemId.ToString();
}

public sealed class XpReward(int count, float? weight, SkillId skillId) : Reward(count, weight)
{
    public SkillId SkillId { get; } = skillId;

    protected override string DisplayId => SkillId.ToString();
}

public sealed class DropTable(params Reward[] rewards)
{
    public Reward[] Rewards { get; } = rewards;
    public float TotalWeight { get; } = rewards.Sum(r => r.Weight ?? 0);

    public override string ToString()
    {
        return $"{{{string.Join(",", Rewards)}}}";
    }
}

public sealed class DropTableService
{
    private readonly Dictionary<DropTableId, DropTable> _dropTables = new();

    public void AddDropTable(DropTableId dropTableId, DropTable dropTable)
    {
        _dropTables.Add(dropTableId, dropTable);
    }

    public Reward RollReward(DropTableId dropTableId)
    {
        if (!_dropTables.TryGetValue(dropTableId, out DropTable? dropTable))
        {
            throw new BackendException($"Drop table id '{dropTableId}' does not have a valid drop table.");
        }

        return RollReward(dropTable);
    }

    public Reward RollReward(DropTable dropTable)
    {
        Reward reward = PickReward(dropTable);
        if (reward is TableReward tableReward)
        {
            return RollReward(tableReward.DropTableId);
        }

        return reward;
    }

    private static Reward PickReward(DropTable dropTable)
    {
        float randomValue = Random.Shared.NextSingle() * dropTable.TotalWeight;
        foreach (Reward reward in dropTable.Rewards)
        {
            randomValue -= reward.Weight ?? 0;
            if (randomValue < 0)
            {
                return reward;
            }
        }

        throw new UnreachableException("We should always pick a reward because randomValue is <= totalWeight.");
    }
}
