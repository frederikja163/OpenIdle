using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class LevelRequirement(SkillId skillId, int level)
{
    public SkillId SkillId { get; } = skillId;
    public int Level { get; } = level;
}

public sealed class ActivityDefinition(Reward[] rewards, LevelRequirement[] requirements)
{
    public Reward[] Rewards { get; } = rewards;
    public LevelRequirement[] Requirements { get; } = requirements;
}

public sealed class ActivityService(IDbContextFactory<GameDbContext> dbContextFactory, DropTableService dropTableService,
    ProfileService profileService, ItemService itemService, SkillService skillService)
{
    private readonly Dictionary<ActivityId, ActivityDefinition> _activities = new();

    public void AddActivity(ActivityId activityId, ActivityDefinition definition)
    {
        _activities.Add(activityId, definition);
    }

    internal async Task<Profile> StartActivityAsync(Guid profileId, ActivityId activityId)
    {
        if (!_activities.TryGetValue(activityId, out ActivityDefinition? definition))
        {
            throw new BackendException($"Activity '{activityId}' does not have a valid definition.");
        }

        Profile profile = await profileService.GetProfileAsync(profileId);

        foreach (LevelRequirement requirement in definition.Requirements)
        {
            Skill[] skills = await skillService.GetSkillsAsync(profileId, [requirement.SkillId]);
            if ((skills.FirstOrDefault()?.Level ?? 0) < requirement.Level)
            {
                throw new BackendException($"Activity '{activityId}' requires {requirement.SkillId} level {requirement.Level}.");
            }
        }

        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Profiles.Attach(profile);
        profile.ActivityId = activityId;
        profile.ActivityStartTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return profile;
    }

    internal async Task<Reward[]> ResolveActivityAsync(Guid profileId)
    {
        Profile profile = await profileService.GetProfileAsync(profileId);

        if (profile.ActivityId is not ActivityId activityId)
        {
            throw new BackendException("Profile is not doing an activity.");
        }

        if (!_activities.TryGetValue(activityId, out ActivityDefinition? definition))
        {
            throw new BackendException($"Activity '{activityId}' does not have a valid definition.");
        }

        Reward[] guaranteedRewards = definition.Rewards.Where(r => r.Weight is null).ToArray();
        Reward[] weightedRewards = definition.Rewards.Where(r => r.Weight is not null).ToArray();

        List<Reward> grantedRewards = [.. guaranteedRewards];
        if (weightedRewards.Length > 0)
        {
            grantedRewards.Add(dropTableService.RollReward(new DropTable(weightedRewards)));
        }

        List<Reward> resolvedRewards = [];
        foreach (Reward reward in grantedRewards)
        {
            Reward resolved = reward switch
            {
                TableReward tableReward => dropTableService.RollReward(tableReward.DropTableId),
                _ => reward,
            };

            switch (resolved)
            {
                case ItemReward itemReward when itemReward.Count > 0:
                    await itemService.AddItemsAsync(profileId, [itemReward]);
                    resolvedRewards.Add(itemReward);
                    break;
                case ItemReward:
                    break;
                case XpReward xpReward:
                    await skillService.AddSkillsAsync(profileId, [xpReward]);
                    resolvedRewards.Add(xpReward);
                    break;
                default:
                    throw new UnreachableException("Rewards should resolve to an item or xp reward.");
            }
        }

        return [.. resolvedRewards];
    }
}
