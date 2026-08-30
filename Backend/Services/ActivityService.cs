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

public sealed class ActivityDefinition(float time, Reward[] rewards, LevelRequirement[] requirements)
{
    public float Time { get; } = time;
    public Reward[] Rewards { get; } = rewards.Where(r => r.Weight is null).ToArray();
    public DropTable? DropTable { get; } = CreateDropTable(rewards);
    
    public LevelRequirement[] Requirements { get; } = requirements;
    
    private static DropTable? CreateDropTable(Reward[] rewards)
    {
        rewards = rewards.Where(r => r.Weight is not null).ToArray();
        return rewards.Length == 0 ? null : new DropTable(rewards);
    }
}

public sealed class ActivityService(IDbContextFactory<GameDbContext> dbContextFactory, DropTableService dropTableService,
    ProfileService profileService, ItemService itemService, SkillService skillService,
    SocketRegistryService socketRegistry, ActivitySchedulerService activitySchedulerService)
{
    private readonly Dictionary<ActivityId, ActivityDefinition> _activities = new();

    public void AddActivity(ActivityId activityId, ActivityDefinition definition)
    {
        _activities.Add(activityId, definition);
    }

    internal async Task<Profile> StartActivityAsync(ProfileId profileId, ActivityId activityId, DateTime? startTime = null)
    {
        if (!_activities.TryGetValue(activityId, out ActivityDefinition? definition))
        {
            throw new BackendException($"Activity '{activityId}' does not have a valid definition.");
        }

        Profile profile = await profileService.GetProfileAsync(profileId);

        if (profile.ActivityId is not null)
        {
            throw new BackendException("Profile is already doing an activity.");
        }

        foreach (LevelRequirement requirement in definition.Requirements)
        {
            Skill[] skills = await skillService.GetSkillsAsync(profileId, [requirement.SkillId]);
            if ((skills.FirstOrDefault()?.Level ?? 0) < requirement.Level)
            {
                throw new BackendException($"Activity '{activityId}' requires {requirement.SkillId} level {requirement.Level}.");
            }
        }

        DateTime startedAt = startTime ?? DateTime.UtcNow;

        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Profiles.Attach(profile);
        profile.ActivityId = activityId;
        profile.ActivityStartTime = startedAt;
        await dbContext.SaveChangesAsync();

        activitySchedulerService.StartEvent(
            new ProfileActivityCompletion(this, socketRegistry, itemService, skillService, profileId, activityId),
            startedAt.AddSeconds(definition.Time));

        return profile;
    }

    internal async Task<Reward[]> ResolveActivityAsync(ProfileId profileId)
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

        activitySchedulerService.RemoveEvent(profileId);

        List<Reward> grantedRewards = [.. definition.Rewards];
        if (definition.DropTable is {})
        {
            grantedRewards.Add(dropTableService.RollReward(definition.DropTable));
        }

        List<Reward> resolvedRewards = [];
        List<ItemReward> itemRewards = [];
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
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
                    itemRewards.Add(itemReward);
                    resolvedRewards.Add(itemReward);
                    break;
                case ItemReward:
                    break;
                case XpReward xpReward when xpReward.Count > 0:
                    await skillService.AddSkillsAsync(dbContext, profileId, [xpReward]);
                    resolvedRewards.Add(xpReward);
                    break;
                case XpReward:
                    break;
                default:
                    throw new UnreachableException("Rewards should resolve to an item or xp reward.");
            }
        }

        if (itemRewards.Count > 0)
        {
            await itemService.AddItemsAsync(dbContext, profileId, itemRewards);
        }

        dbContext.Profiles.Attach(profile);
        profile.ActivityId = null;
        profile.ActivityStartTime = null;

        await dbContext.SaveChangesAsync();
        return [.. resolvedRewards];
    }
}

internal sealed class ProfileActivityCompletion(
    ActivityService activityService, SocketRegistryService socketRegistry, ItemService itemService,
    SkillService skillService, ProfileId profileId, ActivityId activityId)
    : ActivityCompletion(profileId)
{
    public async override Task Complete()
    {
        DateTime completionTime = DateTime.UtcNow;

        await activityService.ResolveActivityAsync(ProfileId);

        // Restart the next cycle immediately, anchored to the exact moment this one ended so the
        // loop has no delay or drift between cycles.
        await activityService.StartActivityAsync(ProfileId, activityId, completionTime);

        Item[] items = await itemService.GetItemsAsync(ProfileId);
        Skill[] skills = await skillService.GetSkillsAsync(ProfileId);
        await socketRegistry.SendToProfileAsync(ProfileId, new ActivityEndedEvent()
        {
            ActivityId = activityId,
            Items = items.Select(i => i.ToDto()).ToArray(),
            Skills = skills.Select(s => s.ToDto()).ToArray(),
        });
    }
}
