using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class ActivityService(IDbContextFactory<GameDbContext> dbContextFactory, DropTableService dropTableService)
{
    private readonly Dictionary<ActivityId, DropTableId> _activities = new();

    public void AddActivity(ActivityId activityId, DropTableId dropTableId)
    {
        _activities.Add(activityId, dropTableId);
    }

    internal async Task<Profile> StartActivityAsync(Profile profile, ActivityId activityId)
    {
        if (!_activities.ContainsKey(activityId))
        {
            throw new BackendException($"Activity '{activityId}' does not have a valid drop table.");
        }

        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Profile dbProfile = await dbContext.Profiles
            .FirstOrDefaultAsync(p => p.ProfileId == profile.ProfileId)
            ?? throw new BackendException("Profile does not exist.");

        dbProfile.ActivityId = activityId;
        dbProfile.ActivityStartTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return dbProfile;
    }

    internal async Task<Item[]> ResolveActivityAsync(Profile profile)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Profile dbProfile = await dbContext.Profiles
            .FirstOrDefaultAsync(p => p.ProfileId == profile.ProfileId)
            ?? throw new BackendException("Profile does not exist.");

        if (dbProfile.ActivityId is not ActivityId activityId)
        {
            throw new BackendException("Profile is not doing an activity.");
        }

        if (!_activities.TryGetValue(activityId, out DropTableId dropTableId))
        {
            throw new BackendException($"Activity '{activityId}' does not have a valid drop table.");
        }

        ItemDrop drop = dropTableService.RollItem(dropTableId);
        return await AddDropAsync(dbContext, dbProfile, drop);
    }

    private static async Task<Item[]> AddDropAsync(GameDbContext dbContext, Profile profile, ItemDrop drop)
    {
        if (drop.Count <= 0)
        {
            return [];
        }

        Item? item = await dbContext.Items
            .FirstOrDefaultAsync(i => i.ProfileId == profile.ProfileId && i.ItemId == drop.ItemId);

        if (item is null)
        {
            item = new Item()
            {
                ProfileId = profile.ProfileId,
                Profile = profile,
                ItemId = drop.ItemId,
                Count = drop.Count,
            };
            dbContext.Items.Add(item);
        }
        else
        {
            item.Count += drop.Count;
        }

        await dbContext.SaveChangesAsync();
        return [item];
    }
}
