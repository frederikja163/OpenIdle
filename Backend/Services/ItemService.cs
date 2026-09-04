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
        var countsByItem = rewards
            .Where(r => r.Count > 0)
            .GroupBy(r => r.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Count));

        List<Item> items = [];
        foreach ((ItemId itemId, int count) in countsByItem)
        {
            Item? item = await dbContext.Items
                .FirstOrDefaultAsync(i => i.ProfileId == profileId && i.ItemId == itemId);

            if (item is null)
            {
                item = new Item()
                {
                    ProfileId = profileId,
                    ItemId = itemId,
                    Count = count,
                };
                dbContext.Items.Add(item);
            }
            else
            {
                item.Count += count;
            }

            items.Add(item);
        }

        return items.ToArray();
    }
}
