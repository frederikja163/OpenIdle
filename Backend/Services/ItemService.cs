using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class ItemService(IDbContextFactory<GameDbContext> dbContextFactory)
{
    internal async Task<Item[]> GetItemsAsync(Guid profileId)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Items
            .Where(i => i.ProfileId == profileId)
            .OrderBy(i => i.ItemId)
            .ToArrayAsync();
    }

    internal async Task<Item[]> GetItemsAsync(Guid profileId, IEnumerable<ItemId> itemIds)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Items
            .Where(i => i.ProfileId == profileId && itemIds.Contains(i.ItemId))
            .OrderBy(i => i.ItemId)
            .ToArrayAsync();
    }

    internal async Task<Item[]> AddItemsAsync(Guid profileId, IEnumerable<ItemReward> rewards)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        List<Item> items = [];
        foreach (ItemReward reward in rewards)
        {
            if (reward.Count <= 0)
            {
                continue;
            }

            Item? item = await dbContext.Items
                .FirstOrDefaultAsync(i => i.ProfileId == profileId && i.ItemId == reward.ItemId);

            if (item is null)
            {
                item = new Item()
                {
                    ProfileId = profileId,
                    ItemId = reward.ItemId,
                    Count = reward.Count,
                };
                dbContext.Items.Add(item);
            }
            else
            {
                item.Count += reward.Count;
            }

            items.Add(item);
        }

        await dbContext.SaveChangesAsync();
        return items.ToArray();
    }
}
