using System;
using Backend.Dtos;

namespace Backend.Database.Entities;

public sealed class Item
{
    public required ProfileId ProfileId { get; init; }
    public Profile? Profile { get; init; }
    public required ItemId ItemId { get; init; }
    public int Count { get; set; }

    public ItemDto ToDto()
    {
        return new ItemDto()
        {
            ProfileId = ProfileId,
            ItemId = ItemId,
            Count = Count,
        };
    }
}
