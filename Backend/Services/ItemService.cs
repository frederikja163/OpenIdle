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
    internal async Task<Item[]> GetItemsAsync(Profile profile)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Items
            .Where(i => i.ProfileId == profile.ProfileId)
            .OrderBy(i => i.ItemId)
            .ToArrayAsync();
    }

    internal async Task<Item[]> GetItemsAsync(Profile profile, IEnumerable<ItemId> itemIds)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Items
            .Where(i => i.ProfileId == profile.ProfileId && itemIds.Contains(i.ItemId))
            .OrderBy(i => i.ItemId)
            .ToArrayAsync();
    }
}
