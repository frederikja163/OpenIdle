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

public sealed class ActivityService
{
    private readonly IDbContextFactory<GameDbContext> _dbContextFactory;
    private readonly DropTableService _dropTableService;
    private readonly ProfileService _profileService;
    private readonly ItemService _itemService;
    private readonly SkillService _skillService;
    private readonly SocketRegistryService _socketRegistry;
    private readonly ActivitySchedulerService _activitySchedulerService;

    private readonly Dictionary<ActivityId, ActivityDefinition> _activities = new();

    public ActivityService(IDbContextFactory<GameDbContext> dbContextFactory, DropTableService dropTableService,
        ProfileService profileService, ItemService itemService, SkillService skillService,
        SocketRegistryService socketRegistry, ActivitySchedulerService activitySchedulerService)
    {
        _dbContextFactory = dbContextFactory;
        _dropTableService = dropTableService;
        _profileService = profileService;
        _itemService = itemService;
        _skillService = skillService;
        _socketRegistry = socketRegistry;
        _activitySchedulerService = activitySchedulerService;

        socketRegistry.ProfileOnline += OnProfileOnline;
        socketRegistry.ProfileOffline += OnProfileOffline;
    }

    public void AddActivity(ActivityId activityId, ActivityDefinition definition)
    {
        _activities.Add(activityId, definition);
    }

    internal Task OnProfileOffline(object? sender, ProfileOfflineEventArgs e)
    {
        _activitySchedulerService.RemoveEvent(e.ProfileId);
        return Task.CompletedTask;
    }

    internal Task OnProfileOnline(object? sender, ProfileOnlineEventArgs e)
    {
        return RescheduleActivityAsync(e.ProfileId);
    }

    private async Task RescheduleActivityAsync(ProfileId profileId)
    {
        Profile profile = await _profileService.GetProfileAsync(profileId);
        if (profile.ActivityId is not { } activityId || profile.ActivityStartTime is not { } startTime)
        {
            return;
        }

        if (!_activities.TryGetValue(activityId, out ActivityDefinition? definition))
        {
            return;
        }

        List<Reward> rewards = [];
        DateTime now = DateTime.UtcNow;
        TimeSpan duration = TimeSpan.FromSeconds(definition.Time);
        while (startTime + duration <= now)
        {
            rewards.AddRange(await ResolveActivityAsync(profileId, startTime + duration));
            startTime += duration;
        }

        if (rewards.Count > 0)
        {
            await SendEventAsync(rewards, profileId, activityId);
        }
        _activitySchedulerService.StartEvent(new ProfileActivityCompletion(this, profileId, activityId, duration), startTime);
    }

    internal async Task<Profile> StartActivityAsync(ProfileId profileId, ActivityId activityId, DateTime? startTime = null)
    {
        if (!_activities.TryGetValue(activityId, out ActivityDefinition? definition))
        {
            throw new BackendException($"Activity '{activityId}' does not have a valid definition.");
        }

        Profile profile = await _profileService.GetProfileAsync(profileId);

        if (profile.ActivityId is not null)
        {
            throw new BackendException("Profile is already doing an activity.");
        }

        foreach (LevelRequirement requirement in definition.Requirements)
        {
            Skill[] skills = await _skillService.GetSkillsAsync(profileId, [requirement.SkillId]);
            if ((skills.FirstOrDefault()?.Level ?? 0) < requirement.Level)
            {
                throw new BackendException($"Activity '{activityId}' requires {requirement.SkillId} level {requirement.Level}.");
            }
        }

        DateTime startedAt = startTime ?? DateTime.UtcNow;
        TimeSpan duration = TimeSpan.FromSeconds(definition.Time);

        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();
        dbContext.Profiles.Attach(profile);
        profile.ActivityId = activityId;
        profile.ActivityStartTime = startedAt;
        await dbContext.SaveChangesAsync();

        _activitySchedulerService.StartEvent(
            new ProfileActivityCompletion(this, profileId, activityId, duration), startedAt);

        return profile;
    }

    internal async Task<List<Reward>> ResolveActivityAsync(ProfileId profileId, DateTime endTime)
    {
        Profile profile = await _profileService.GetProfileAsync(profileId);

        if (profile.ActivityId is not { } activityId)
        {
            throw new BackendException("Profile is not doing an activity.");
        }

        if (!_activities.TryGetValue(activityId, out ActivityDefinition? definition))
        {
            throw new BackendException($"Activity '{activityId}' does not have a valid definition.");
        }

        List<Reward> grantedRewards = [.. definition.Rewards];
        if (definition.DropTable is {})
        {
            grantedRewards.Add(_dropTableService.RollReward(definition.DropTable));
        }

        List<Reward> resolvedRewards = [];
        List<ItemReward> itemRewards = [];
        List<XpReward> xpRewards = [];
        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();
        foreach (Reward reward in grantedRewards)
        {
            Reward resolved = reward switch
            {
                TableReward tableReward => _dropTableService.RollReward(tableReward.DropTableId),
                _ => reward,
            };

            switch (resolved)
            {
                case ItemReward { Count: >= 0 } itemReward:
                    itemRewards.Add(itemReward);
                    resolvedRewards.Add(itemReward);
                    break;
                case XpReward { Count: >= 0 } xpReward:
                    xpRewards.Add(xpReward);
                    resolvedRewards.Add(xpReward);
                    break;
                default:
                    throw new UnreachableException("Rewards should resolve to an item or xp reward, with a non negative count.");
            }
        }

        if (itemRewards.Count > 0)
        {
            await _itemService.AddItemsAsync(dbContext, profileId, itemRewards);
        }

        if (xpRewards.Count > 0)
        {
            await _skillService.AddSkillsAsync(dbContext, profileId, xpRewards);
        }

        dbContext.Attach(profile);
        profile.ActivityStartTime = endTime;

        await dbContext.SaveChangesAsync();
        return [.. resolvedRewards];
    }

    internal async Task SendEventAsync(List<Reward> rewards, ProfileId profileId, ActivityId activityId)
    {
        Item[] items = await _itemService.GetItemsAsync(profileId, rewards.OfType<ItemReward>().Select(i => i.ItemId).Distinct());
        Skill[] skills = await _skillService.GetSkillsAsync(profileId, rewards.OfType<XpReward>().Select(x => x.SkillId).Distinct());
        await _socketRegistry.SendToProfileAsync(profileId, new ActivityEndedEvent
        {
            ActivityId = activityId,
            Items = items.Select(i => i.ToDto()).ToArray(),
            Skills = skills.Select(s => s.ToDto()).ToArray(),
        });
    }
}

internal sealed class ProfileActivityCompletion(
    ActivityService activityService, ProfileId profileId, ActivityId activityId, TimeSpan duration)
    : ActivityCompletion(profileId, duration)
{
    public override async Task Complete(DateTime endTime)
    {
        List<Reward> rewards = await activityService.ResolveActivityAsync(ProfileId, endTime);
        await activityService.SendEventAsync(rewards, ProfileId, activityId);
    }
}
