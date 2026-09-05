using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Backend.Services;

public sealed class LevelRequirement(SkillId skillId, int level)
{
    public SkillId SkillId { get; } = skillId;
    public int Level { get; } = level;
}

public sealed class ItemCost(int count, ItemId itemId)
{
    public int Count { get; } = count;
    public ItemId ItemId { get; } = itemId;
}

public sealed class ActivityDefinition(float time, Reward[] rewards, LevelRequirement[] requirements, ItemCost[]? costs = null)
{
    public float Time { get; } = time;
    public Reward[] Rewards { get; } = rewards.Where(r => r.Weight is null).ToArray();
    public DropTable? DropTable { get; } = CreateDropTable(rewards);
    
    public LevelRequirement[] Requirements { get; } = requirements;
    public ItemCost[] Costs { get; } = costs ?? [];
    
    private static DropTable? CreateDropTable(Reward[] rewards)
    {
        rewards = rewards.Where(r => r.Weight is not null).ToArray();
        return rewards.Length == 0 ? null : new DropTable(rewards);
    }
}

public sealed class RewardCollection
{
    private readonly Dictionary<ItemId, int> _itemRewards = new();
    private readonly Dictionary<SkillId, int>  _skillRewards = new();

    public int TotalActivities
    {
        get;
        set;
    }

    public void AddReward(Reward reward)
    {
        switch (reward)
        {
            case ItemReward itemReward:
                int items = itemReward.Count + (_itemRewards.TryGetValue(itemReward.ItemId, out int existing)
                    ? existing
                    : 0);
                _itemRewards[itemReward.ItemId] = items;
                break;
            case XpReward xpReward:
                int xp = xpReward.Count + (_skillRewards.TryGetValue(xpReward.SkillId, out existing) ? existing : 0);
                _skillRewards[xpReward.SkillId] = xp;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reward));
        }
    }

    public IEnumerable<ItemReward> GetItems()
    {
        return _itemRewards.Select(kvp => new ItemReward(kvp.Value, 1, kvp.Key));
    }

    public IEnumerable<XpReward> GetSkills()
    {
        return _skillRewards.Select(kvp => new XpReward(kvp.Value, 1, kvp.Key));
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

        Item[] ownedItems = await _itemService.GetItemsAsync(profileId);
        Dictionary<ItemId, int> itemCache = ownedItems.ToDictionary(item => item.ItemId, item => item.Count);

        RewardCollection rewards = new RewardCollection();
        DateTime now = DateTime.UtcNow;
        TimeSpan duration = TimeSpan.FromSeconds(definition.Time);
        DateTime endTime = startTime + duration;
        bool ranOutOfItems = false;
        while (endTime <= now)
        {
            if (!CanAffordCost(itemCache, definition))
            {
                ranOutOfItems = true;
                break;
            }

            foreach (ItemReward itemReward in RollRewards(rewards, definition))
            {
                itemCache[itemReward.ItemId] = itemCache.GetValueOrDefault(itemReward.ItemId) + itemReward.Count;
            }
            foreach (ItemCost cost in definition.Costs)
            {
                itemCache[cost.ItemId] = itemCache.GetValueOrDefault(cost.ItemId) - cost.Count;
            }
            endTime += duration;
        }

        if (rewards.TotalActivities > 0)
        {
            await ResolveRewardCollection(rewards, profileId, endTime, activityId);
        }

        if (ranOutOfItems)
        {
            _activitySchedulerService.RemoveEvent(profileId);
            await _profileService.ClearActivityAsync(profileId);
            return;
        }

        _activitySchedulerService.StartEvent(new ProfileActivityCompletion(this, profileId, activityId, duration), endTime);
    }

    private static bool CanAffordCost(Dictionary<ItemId, int> itemCache, ActivityDefinition definition)
    {
        foreach (ItemCost cost in definition.Costs)
        {
            if (itemCache.GetValueOrDefault(cost.ItemId) < cost.Count)
            {
                return false;
            }
        }

        return true;
    }

    internal async Task<bool> CanAffordActivityAsync(ProfileId profileId, ActivityId activityId)
    {
        if (!_activities.TryGetValue(activityId, out ActivityDefinition? definition) || definition.Costs.Length == 0)
        {
            return true;
        }

        Item[] ownedItems = await _itemService.GetItemsAsync(
            profileId, definition.Costs.Select(cost => cost.ItemId));

        foreach (ItemCost cost in definition.Costs)
        {
            int owned = ownedItems.FirstOrDefault(item => item.ItemId == cost.ItemId)?.Count ?? 0;
            if (owned < cost.Count)
            {
                return false;
            }
        }

        return true;
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

        if (!await CanAffordActivityAsync(profileId, activityId))
        {
            ItemCost cost = definition.Costs.First();
            throw new BackendException($"Activity '{activityId}' requires {cost.Count} of {cost.ItemId}.");
        }

        DateTime startedAt = startTime ?? DateTime.UtcNow;
        TimeSpan duration = TimeSpan.FromSeconds(definition.Time);

        await _profileService.SetActivityAsync(profileId, activityId, startedAt);
        profile.ActivityId = activityId;
        profile.ActivityStartTime = startedAt;

        _activitySchedulerService.StartEvent(
            new ProfileActivityCompletion(this, profileId, activityId, duration), startedAt);

        return profile;
    }

    internal async Task StopActivityAsync(ProfileId profileId)
    {
        Profile profile = await _profileService.GetProfileAsync(profileId);

        if (profile.ActivityId is null)
        {
            throw new BackendException("Profile is not doing an activity.");
        }

        _activitySchedulerService.RemoveEvent(profileId);

        await _profileService.ClearActivityAsync(profileId);
    }

    internal async Task ResolveActivityAsync(ProfileId profileId, RewardCollection rewardCollection)
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

        RollRewards(rewardCollection, definition);
    }

    private IEnumerable<ItemReward> RollRewards(RewardCollection rewardCollection, ActivityDefinition definition)
    {
        List<Reward> grantedRewards = [.. definition.Rewards];
        if (definition.DropTable is {})
        {
            grantedRewards.Add(_dropTableService.RollReward(definition.DropTable));
        }

        List<ItemReward> itemRewards = [];
        foreach (Reward reward in grantedRewards)
        {
            Reward resolved = reward switch
            {
                TableReward tableReward => _dropTableService.RollReward(tableReward.DropTableId),
                _ => reward,
            };

            switch (resolved)
            {
                case ItemReward itemReward:
                    rewardCollection.AddReward(itemReward);
                    itemRewards.Add(itemReward);
                    break;
                case XpReward xpReward:
                    rewardCollection.AddReward(xpReward);
                    break;
                default:
                    throw new UnreachableException("Rewards should resolve to an item or xp reward, with a non negative count.");
            }
        }

        rewardCollection.TotalActivities++;
        return itemRewards;
    }

    internal async Task ResolveRewardCollection(RewardCollection rewards, ProfileId profileId, DateTime startTime, ActivityId activityId)
    {
        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();
        await _profileService.SetActivityAsync(dbContext, profileId, activityId, startTime);
        ItemCost[] costs = _activities.TryGetValue(activityId, out ActivityDefinition? definition) ? definition.Costs : [];
        Item[] items = await _itemService.ApplyItemDeltaAsync(dbContext, profileId, rewards.GetItems(), costs, rewards.TotalActivities);
        Skill[] skills = await _skillService.AddSkillsAsync(dbContext, profileId, rewards.GetSkills());
        await dbContext.SaveChangesAsync();
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
        if (!await activityService.CanAffordActivityAsync(ProfileId, activityId))
        {
            await activityService.StopActivityAsync(ProfileId);
            return;
        }

        RewardCollection rewards = new();
        await activityService.ResolveActivityAsync(ProfileId, rewards);
        await activityService.ResolveRewardCollection(rewards, ProfileId, endTime, activityId);
    }
}
