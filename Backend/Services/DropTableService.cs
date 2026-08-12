using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Backend.Dtos;

namespace Backend.Services;

public abstract class WeightedDrop(int count, float weight)
{
    public int Count { get; } = count;
    public float Weight { get; } = weight;

    protected abstract string DisplayId { get; }

    public override string ToString()
    {
        return $"[{Weight}|{Count}x{DisplayId}]";
    }
}

public sealed class TableDrop(int count, float weight, DropTableId dropTableId) : WeightedDrop(count, weight)
{
    public DropTableId DropTableId { get; } = dropTableId;

    protected override string DisplayId => DropTableId.ToString();
}

public sealed class ItemDrop(int count, float weight, ItemId itemId) : WeightedDrop(count, weight)
{
    public ItemId ItemId { get; } = itemId;

    protected override string DisplayId => ItemId.ToString();
}

public sealed class DropTable(params WeightedDrop[] drops)
{
    public WeightedDrop[] Drops { get; } = drops;
    public float TotalWeight { get; } = drops.Sum(d => d.Weight);

    public override string ToString()
    {
        return $"{{{string.Join(",", Drops)}}}";
    }
}

public sealed class DropTableService
{
    private readonly Dictionary<DropTableId, DropTable> _dropTables = new();

    public void AddDropTable(DropTableId dropTableId, DropTable dropTable)
    {
        _dropTables.Add(dropTableId, dropTable);
    }

    public ItemDrop RollItem(DropTableId dropTableId)
    {
        if (!_dropTables.TryGetValue(dropTableId, out DropTable? dropTable))
        {
            throw new BackendException($"Drop table id '{dropTableId}' does not have a valid drop table.");
        }

        WeightedDrop drop = GetItem(dropTable);
        switch (drop)
        {
            case TableDrop tableDrop:
                return RollItem(tableDrop.DropTableId);
            case ItemDrop itemDrop:
                return itemDrop;
            default:
                throw new UnreachableException("Drop should only be able to be TableDrop or ItemDrop");
        }
    }

    private static WeightedDrop GetItem(DropTable dropTable)
    {
        float randomValue = Random.Shared.NextSingle() * dropTable.TotalWeight;
        foreach (WeightedDrop drop in dropTable.Drops)
        {
            randomValue -= drop.Weight;
            if (randomValue < 0)
            {
                return drop;
            }
        }

        throw new UnreachableException("We should always pick a drop because randomValue is <= totalWeight.");
    }
}
