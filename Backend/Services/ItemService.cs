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
    internal async Task<Item[]> GetItemsAsync(ProfileId profileId)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Items
            .Where(i => i.ProfileId == profileId)
            .OrderBy(i => i.ItemId)
            .ToArrayAsync();
    }

    internal async Task<Item[]> GetItemsAsync(ProfileId profileId, IEnumerable<ItemId> itemIds)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Items
            .Where(i => i.ProfileId == profileId && itemIds.Contains(i.ItemId))
            .OrderBy(i => i.ItemId)
            .ToArrayAsync();
    }

    internal async Task<Item[]> AddItemsAsync(ProfileId profileId, IEnumerable<ItemReward> rewards)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Item[] items = await AddItemsAsync(dbContext, profileId, rewards);
        await dbContext.SaveChangesAsync();
        return items;
    }

    internal async Task<Item[]> AddItemsAsync(GameDbContext dbContext, ProfileId profileId, IEnumerable<ItemReward> rewards)
    {
        return await ApplyItemDeltaAsync(dbContext, profileId, rewards, costs: null, completions: 0);
    }

    internal async Task<Item[]> ApplyItemDeltaAsync(
        GameDbContext dbContext, ProfileId profileId, IEnumerable<ItemReward> rewards, IEnumerable<ItemCost>? costs, int completions)
    {
        var countsByItem = rewards
            .Where(r => r.Count > 0)
            .GroupBy(r => r.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Count));

        if (costs is not null)
        {
            foreach (ItemCost cost in costs)
            {
                countsByItem[cost.ItemId] = countsByItem.GetValueOrDefault(cost.ItemId) - cost.Count * completions;
            }
        }

        List<Item> items = [];
        foreach ((ItemId itemId, int delta) in countsByItem)
        {
            Item? item = await dbContext.Items
                .FirstOrDefaultAsync(i => i.ProfileId == profileId && i.ItemId == itemId);

            int finalCount = (item?.Count ?? 0) + delta;
            if (finalCount <= 0)
            {
                if (item is not null)
                {
                    dbContext.Items.Remove(item);
                    items.Add(new Item()
                    {
                        ProfileId = profileId,
                        ItemId = itemId,
                        Count = 0,
                    });
                }
                continue;
            }

            if (item is null)
            {
                item = new Item()
                {
                    ProfileId = profileId,
                    ItemId = itemId,
                    Count = finalCount,
                };
                dbContext.Items.Add(item);
            }
            else
            {
                item.Count = finalCount;
            }

            items.Add(item);
        }

        return items.ToArray();
    }
}
