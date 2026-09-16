using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;
using OpenIdle.Tests.Database;
using OpenIdle.Tests.TestDoubles;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class ActivityServiceTests : IDisposable
{
    private readonly TestGameDb _db = new();

    public void Dispose()
    {
        _db.Dispose();
    }

    [Test]
    public async Task StartActivityAsync_SetsActivityAndStartTime()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _) = CreateService();
        AddMineTinActivity(service);

        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        Profile returned = await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);
        DateTime after = DateTime.UtcNow.AddSeconds(1);

        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Profile updated = (await dbContext.Profiles.FindAsync(profile.ProfileId))!;
        Assert.Multiple(() =>
        {
            Assert.That(updated.ActivityId, Is.EqualTo(ActivityId.MineTin));
            Assert.That(updated.ActivityStartTime, Is.Not.Null);
            Assert.That(updated.ActivityStartTime, Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(after));
            Assert.That(returned.ActivityId, Is.EqualTo(ActivityId.MineTin));
            Assert.That(returned.ActivityStartTime, Is.EqualTo(updated.ActivityStartTime));
        });
    }

    [Test]
    public async Task StartActivityAsync_UnknownActivity_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();

        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Does.Contain("valid definition"));
        });
    }

    [Test]
    public async Task StartActivityAsync_NonexistentProfile_ThrowsBackendException()
    {
        (ActivityService service, _) = CreateService();
        AddMineTinActivity(service);
        Profile profile = new()
        {
            Name = "Ghost",
            ProfileId = Guid.NewGuid(),
            CreationTime = DateTime.UtcNow,
            LastActiveTime = DateTime.UtcNow
        };

        Assert.ThrowsAsync<BackendException>(() => service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin));
    }

    [Test]
    public async Task StartActivityAsync_AlreadyDoingActivity_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _) = CreateService();
        AddMineTinActivity(service);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Does.Contain("already doing an activity"));
        });
    }

    [Test]
    public async Task StartActivityAsync_MissingCostItem_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Tin)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)],
            costs: [new ItemCost(1, ItemId.Cedar)]));

        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Does.Contain("requires 1 of Cedar"));
        });
        Assert.That(await GetItemsAsync(profile.ProfileId), Is.Empty);
    }

    [Test]
    public async Task StartActivityAsync_InsufficientCostItem_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 1);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Tin)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)],
            costs: [new ItemCost(2, ItemId.Cedar)]));

        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Does.Contain("requires 2 of Cedar"));
        });
        Item food = (await GetItemsAsync(profile.ProfileId)).Single();
        Assert.That(food.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task StartActivityAsync_SatisfiesCostItem_DoesNotDeductAtStart()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 3);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Tin)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)],
            costs: [new ItemCost(1, ItemId.Cedar)]));

        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        Item food = (await GetItemsAsync(profile.ProfileId)).Single();
        Assert.That(food.ItemId, Is.EqualTo(ItemId.Cedar));
        Assert.That(food.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task ResolveRewardCollection_DeductsCostPerCompletedActivity()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 10);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Tin)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)],
            costs: [new ItemCost(1, ItemId.Cedar)]));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        RewardCollection rewards = new();
        await service.ResolveActivityAsync(profile.ProfileId, rewards);
        await service.ResolveActivityAsync(profile.ProfileId, rewards);
        await service.ResolveRewardCollection(rewards, profile.ProfileId, DateTime.UtcNow, ActivityId.MineTin);

        await Assert.MultipleAsync(async () =>
        {
            Item stone = (await GetItemsAsync(profile.ProfileId)).Single(i => i.ItemId == ItemId.Tin);
            Assert.That(stone.Count, Is.EqualTo(8));
            Item food = (await GetItemsAsync(profile.ProfileId)).Single(i => i.ItemId == ItemId.Cedar);
            Assert.That(food.Count, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task Completion_WhenFoodRunsOut_StopsActivityWithoutRewards()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 2);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _, _, _) = CreateServiceWithInternals();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Tin)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)],
            costs: [new ItemCost(1, ItemId.Cedar)]));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        ProfileActivityCompletion completion = new(service, profile.ProfileId, ActivityId.MineTin, TimeSpan.FromSeconds(1));

        await completion.Complete(DateTime.UtcNow);
        await completion.Complete(DateTime.UtcNow);

        Item[] afterTwo = await GetItemsAsync(profile.ProfileId);
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Profile afterTwoProfile = (await dbContext.Profiles.FindAsync(profile.ProfileId))!;
        Assert.Multiple(() =>
        {
            Item stone = afterTwo.Single(i => i.ItemId == ItemId.Tin);
            Assert.That(stone.Count, Is.EqualTo(8));
            Assert.That(afterTwo.Any(i => i.ItemId == ItemId.Cedar), Is.False);
            Assert.That(afterTwoProfile.ActivityId, Is.Not.Null);
        });

        await completion.Complete(DateTime.UtcNow);

        Item[] afterThree = await GetItemsAsync(profile.ProfileId);
        await using GameDbContext freshContext = await _db.Factory.CreateDbContextAsync();
        Profile stopped = (await freshContext.Profiles.FindAsync(profile.ProfileId))!;
        Assert.Multiple(() =>
        {
            Item stone = afterThree.Single(i => i.ItemId == ItemId.Tin);
            Assert.That(stone.Count, Is.EqualTo(8));
            Assert.That(stopped.ActivityId, Is.Null);
        });
    }

    [Test]
    public async Task StopActivityAsync_ClearsActivityAndRemovesScheduledEvent()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _, ActivitySchedulerService scheduler, _) = CreateServiceWithInternals();
        AddMineTinActivity(service, time: 10f);

        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(-100));
        await service.StopActivityAsync(profile.ProfileId);

        await scheduler.NextEvent();

        Item[] items = await GetItemsAsync(profile.ProfileId);
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Profile updated = (await dbContext.Profiles.FindAsync(profile.ProfileId))!;
        Assert.Multiple(() =>
        {
            Assert.That(items, Is.Empty);
            Assert.That(updated.ActivityId, Is.Null);
            Assert.That(updated.ActivityStartTime, Is.Null);
        });
    }

    [Test]
    public async Task StopActivityAsync_NotDoingActivity_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();

        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.StopActivityAsync(profile.ProfileId));

        Assert.That(exception?.Message, Does.Contain("not doing an activity"));
    }

    [Test]
    public async Task StartActivityAsync_ExplicitStartTime_IsUsedAsAnchor()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _) = CreateService();
        AddMineTinActivity(service);

        DateTime anchor = DateTime.UtcNow.AddSeconds(-30);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, anchor);

        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Profile updated = (await dbContext.Profiles.FindAsync(profile.ProfileId))!;
        Assert.That(updated.ActivityStartTime, Is.EqualTo(anchor));
    }

    [Test]
    public async Task ResolveActivityAsync_NoActivity_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        AddMineTinActivity(service);

        RewardCollection rewards = new();
        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.ResolveActivityAsync(profile.ProfileId, rewards));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Is.EqualTo("Profile is not doing an activity."));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_ExistingItemAndXpAreIncremented()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Tin, 1);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 5, level: 1);
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Tin), new XpReward(10, null, SkillId.Mining)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        RewardCollection rewards = new();
        await service.ResolveActivityAsync(profile.ProfileId, rewards);
        await service.ResolveActivityAsync(profile.ProfileId, rewards);
        await service.ResolveRewardCollection(rewards, profile.ProfileId, DateTime.UtcNow, ActivityId.MineTin);

        await Assert.MultipleAsync(async () =>
        {
            Item item = (await GetItemsAsync(profile.ProfileId)).Single();
            Assert.That(item.Count, Is.EqualTo(9));
            Skill skill = (await GetSkillsAsync(profile.ProfileId)).Single();
            Assert.That(skill.Xp, Is.EqualTo(25));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_GuaranteedTableReward_RollsTheTable()
    {
        Profile profile = await SeedProfileAsync();
        DropTableService dropTableService = new();
        dropTableService.AddDropTable(DropTableId.TinBonusTable, new DropTable(new ItemReward(1, 1, ItemId.Tin)));
        SocketRegistryService socketRegistry = new();
        ActivityService service = new(_db.Factory, dropTableService, new ProfileService(_db.Factory, socketRegistry), new ItemService(_db.Factory), new SkillService(_db.Factory), socketRegistry, new ActivitySchedulerService());
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new TableReward(1, null, DropTableId.TinBonusTable)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        RewardCollection rewards = new();
        await service.ResolveActivityAsync(profile.ProfileId, rewards);
        await service.ResolveRewardCollection(rewards, profile.ProfileId, DateTime.UtcNow, ActivityId.MineTin);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(rewards.GetItems().Count(), Is.EqualTo(1));
            Assert.That(rewards.GetSkills(), Is.Empty);
            Item item = (await GetItemsAsync(profile.ProfileId)).Single();
            Assert.That(item.ItemId, Is.EqualTo(ItemId.Tin));
            Assert.That(item.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_WeightedRewards_RollsExactlyOne()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(2, 5, ItemId.Tin), new XpReward(10, 5, SkillId.Mining)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        RewardCollection rewards = new();
        await service.ResolveActivityAsync(profile.ProfileId, rewards);

        Assert.That(rewards.GetItems().Count() + rewards.GetSkills().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ResolveActivityAsync_GuaranteedAndWeightedRewards_AllGranted()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Tin), new ItemReward(2, 5, ItemId.Balsa), new XpReward(10, 5, SkillId.Mining)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        RewardCollection rewards = new();
        await service.ResolveActivityAsync(profile.ProfileId, rewards);
        await service.ResolveRewardCollection(rewards, profile.ProfileId, DateTime.UtcNow, ActivityId.MineTin);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(rewards.GetItems().Count() + rewards.GetSkills().Count(), Is.EqualTo(2));
            ItemReward stoneReward = rewards.GetItems().Single(r => r.ItemId == ItemId.Tin);
            Assert.That(stoneReward.Count, Is.EqualTo(4));
            Item stone = (await GetItemsAsync(profile.ProfileId)).Single(i => i.ItemId == ItemId.Tin);
            Assert.That(stone.Count, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_ZeroCountItem_AddsNothing()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(0, null, ItemId.None)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        RewardCollection rewards = new();
        await service.ResolveActivityAsync(profile.ProfileId, rewards);
        await service.ResolveRewardCollection(rewards, profile.ProfileId, DateTime.UtcNow, ActivityId.MineTin);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(rewards.GetItems().Single().Count, Is.EqualTo(0));
            Assert.That(await GetItemsAsync(profile.ProfileId), Is.Empty);
        });
    }

    [Test]
    public void AddActivity_DuplicateKey_ThrowsArgumentException()
    {
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(time: 1f, rewards: [], requirements: []));

        Assert.Throws<ArgumentException>(() =>
            service.AddActivity(ActivityId.MineTin, new ActivityDefinition(time: 1f, rewards: [], requirements: [])));
    }

    private static void AddMineTinActivity(ActivityService service, float time = 1f)
    {
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: time,
            rewards: [new ItemReward(4, null, ItemId.Tin), new XpReward(10, null, SkillId.Mining)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)]));
    }

    private static async Task<FakeWebSocket> RegisterSocket(SocketRegistryService socketRegistry, Guid profileId)
    {
        FakeWebSocket webSocket = new();
        Socket socket = new Socket(webSocket);
        socketRegistry.RegisterSocket(socket);
        await socketRegistry.SetProfile(socket, profileId);
        return webSocket;
    }

    [Test]
    public async Task ProfileOffline_RemovesScheduledEvent_AndOnlineResolves()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _, ActivitySchedulerService scheduler, _) = CreateServiceWithInternals();
        AddMineTinActivity(service, time: 10f);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(-100));

        await service.OnProfileOffline(this, new ProfileOfflineEventArgs(profile.ProfileId));
        await scheduler.NextEvent();

        Assert.That(await GetItemsAsync(profile.ProfileId), Is.Empty);

        await service.OnProfileOnline(this, new ProfileOnlineEventArgs(profile.ProfileId));

        Item item = (await GetItemsAsync(profile.ProfileId)).Single();
        Assert.That(item.ItemId, Is.EqualTo(ItemId.Tin));
        Assert.That(item.Count, Is.EqualTo(40));
    }

    [Test]
    public async Task Reschedule_WithRewardFunding_FarmsAllDueCompletions()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 2);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _, _, _) = CreateServiceWithInternals();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 10f,
            rewards: [new ItemReward(3, null, ItemId.Cedar), new XpReward(10, null, SkillId.Mining)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)],
            costs: [new ItemCost(1, ItemId.Cedar)]));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(-100));

        await service.OnProfileOffline(this, new ProfileOfflineEventArgs(profile.ProfileId));
        await service.OnProfileOnline(this, new ProfileOnlineEventArgs(profile.ProfileId));

        Item food = (await GetItemsAsync(profile.ProfileId)).Single(i => i.ItemId == ItemId.Cedar);
        Assert.That(food.Count, Is.EqualTo(22));
    }

    [Test]
    public async Task Reschedule_WhenFoodRunsOutOnlyResolvesAffordableCompletions()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 2);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _, _, _) = CreateServiceWithInternals();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 10f,
            rewards: [new XpReward(10, null, SkillId.Mining)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)],
            costs: [new ItemCost(1, ItemId.Cedar)]));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(-100));

        await service.OnProfileOffline(this, new ProfileOfflineEventArgs(profile.ProfileId));
        await service.OnProfileOnline(this, new ProfileOnlineEventArgs(profile.ProfileId));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetItemsAsync(profile.ProfileId), Is.Empty);
            await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
            Profile cleared = (await dbContext.Profiles.FindAsync(profile.ProfileId))!;
            Assert.That(cleared.ActivityId, Is.Null);
        });
    }

    [Test]
    public void ItemCost_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.That(() => new ItemCost(-1, ItemId.Cedar), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => new ItemCost(0, ItemId.Cedar), Throws.Nothing);
    }

    [Test]
    public async Task ResolveRewardCollection_WhenActivityStopped_DoesNotGrantRewards()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 5);
        (ActivityService service, _, _, _) = CreateServiceWithInternals();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 10f,
            rewards: [new ItemReward(4, null, ItemId.Tin)],
            requirements: [],
            costs: [new ItemCost(1, ItemId.Cedar)]));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin);

        RewardCollection rewards = new();
        await service.ResolveActivityAsync(profile.ProfileId, rewards);
        await service.StopActivityAsync(profile.ProfileId);
        await service.ResolveRewardCollection(rewards, profile.ProfileId, DateTime.UtcNow, ActivityId.MineTin);

        await Assert.MultipleAsync(async () =>
        {
            Item food = (await GetItemsAsync(profile.ProfileId)).Single(i => i.ItemId == ItemId.Cedar);
            Assert.That(food.Count, Is.EqualTo(5));
            Assert.That(await GetItemsAsync(profile.ProfileId), Has.None.Matches<Item>(i => i.ItemId == ItemId.Tin));
            await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
            Profile cleared = (await dbContext.Profiles.FindAsync(profile.ProfileId))!;
            Assert.That(cleared.ActivityId, Is.Null);
            Assert.That(cleared.ActivityStartTime, Is.Null);
        });
    }

    [Test]
    public async Task Completion_ConsumingLastItem_ReportsZeroInEvent()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 1);
        (ActivityService service, SocketRegistryService socketRegistry, ActivitySchedulerService scheduler, _) =
            CreateServiceWithInternals();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(
            time: 10f,
            rewards: [new ItemReward(4, null, ItemId.Tin)],
            requirements: [],
            costs: [new ItemCost(1, ItemId.Cedar)]));
        FakeWebSocket webSocket = await RegisterSocket(socketRegistry, profile.ProfileId);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(-100));

        await scheduler.NextEvent();

        Assert.That(webSocket.FirstSentText, Does.Contain("ActivityEndedEvent"));
        Assert.That(webSocket.FirstSentText, Does.Contain("\"itemId\":\"Cedar\",\"count\":0"));
        Assert.That(webSocket.FirstSentText, Does.Contain("\"itemId\":\"Tin\",\"count\":4"));
        Item[] items = await GetItemsAsync(profile.ProfileId);
        Assert.That(items, Has.Length.EqualTo(1));
        Assert.That(items.Single().ItemId, Is.EqualTo(ItemId.Tin));
        Assert.That(items.Single().Count, Is.EqualTo(4));
    }

    [Test]
    public async Task ProfileOnline_ReschedulesFutureActivity_DoesNotCompleteEarly()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _, ActivitySchedulerService scheduler, _) = CreateServiceWithInternals();
        AddMineTinActivity(service);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(60));

        await service.OnProfileOffline(this, new ProfileOfflineEventArgs(profile.ProfileId));
        await service.OnProfileOnline(this, new ProfileOnlineEventArgs(profile.ProfileId));

        await scheduler.NextEvent();

        Assert.That(await GetItemsAsync(profile.ProfileId), Is.Empty);
    }

    [Test]
    public async Task ProfileOnline_FutureActivity_DoesNotSendEvent()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, SocketRegistryService socketRegistry, _, _) =
            CreateServiceWithInternals();
        AddMineTinActivity(service);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(60));

        FakeWebSocket webSocket = await RegisterSocket(socketRegistry, profile.ProfileId);

        Assert.That(webSocket.FirstSentText, Is.Null);
    }

    [Test]
    public async Task ProfileOnline_ElapsedActivityWithoutRewards_SendsEmptyEvent()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, SocketRegistryService socketRegistry, _, _) =
            CreateServiceWithInternals();
        service.AddActivity(ActivityId.MineTin, new ActivityDefinition(time: 10f, rewards: [], requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(-100));

        FakeWebSocket webSocket = await RegisterSocket(socketRegistry, profile.ProfileId);

        Assert.That(webSocket.FirstSentText, Does.Contain("ActivityEndedEvent"));
    }

    [Test]
    public async Task ProfileOnline_ResolvesExpiredActivity_CompletesAndSendsEvent()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, SocketRegistryService socketRegistry, _, _) =
            CreateServiceWithInternals();
        AddMineTinActivity(service, time: 10f);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        FakeWebSocket webSocket = await RegisterSocket(socketRegistry, profile.ProfileId);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(-100));

        await service.OnProfileOffline(this, new ProfileOfflineEventArgs(profile.ProfileId));
        await service.OnProfileOnline(this, new ProfileOnlineEventArgs(profile.ProfileId));

        Assert.That(webSocket.FirstSentText, Does.Contain("ActivityEndedEvent"));
        Item item = (await GetItemsAsync(profile.ProfileId)).Single();
        Assert.That(item.Count, Is.EqualTo(40));
    }

    [Test]
    public async Task ProfileOnline_CatchUp_AnchorsToCurrentCycleStart()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _, ActivitySchedulerService scheduler, _) = CreateServiceWithInternals();
        AddMineTinActivity(service, time: 10f);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        // Two full cycles elapsed, five seconds into the third.
        DateTime before = DateTime.UtcNow;
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, before.AddSeconds(-25));

        await service.OnProfileOffline(this, new ProfileOfflineEventArgs(profile.ProfileId));
        await service.OnProfileOnline(this, new ProfileOnlineEventArgs(profile.ProfileId));
        await scheduler.NextEvent();

        await Assert.MultipleAsync(async () =>
        {
            Item item = (await GetItemsAsync(profile.ProfileId)).Single();
            Assert.That(item.Count, Is.EqualTo(8));
            await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
            Profile updated = (await dbContext.Profiles.FindAsync(profile.ProfileId))!;
            Assert.That(updated.ActivityStartTime, Is.EqualTo(before.AddSeconds(-5)).Within(TimeSpan.FromSeconds(1)));
        });
    }

    [Test]
    [Category("Perf")]
    public async Task RescheduleActivityAsync_ElapsedLongOffline_Benchmark()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _, _, _) = CreateServiceWithInternals();
        AddMineTinActivity(service);

        const int actions = 1_000_000;
        await service.StartActivityAsync(profile.ProfileId, ActivityId.MineTin, DateTime.UtcNow.AddSeconds(-actions));
        await service.OnProfileOffline(this, new ProfileOfflineEventArgs(profile.ProfileId));

        Stopwatch stopwatch = Stopwatch.StartNew();
        await service.OnProfileOnline(this, new ProfileOnlineEventArgs(profile.ProfileId));
        stopwatch.Stop();

        TestContext.Progress.WriteLine(
            $"RescheduleActivityAsync completed {actions} actions in {stopwatch.ElapsedMilliseconds} ms ({stopwatch.ElapsedMilliseconds / (double)actions:F2} ms/action)");

        await Assert.MultipleAsync(async () =>
        {
            Item item = (await GetItemsAsync(profile.ProfileId)).Single();
            Assert.That(item.Count, Is.GreaterThanOrEqualTo(actions * 4));
            Skill skill = (await GetSkillsAsync(profile.ProfileId)).Single();
            Assert.That(skill.Xp, Is.GreaterThanOrEqualTo(actions * 10));
        });
    }

    private (ActivityService, SocketRegistryService, ActivitySchedulerService, DropTableService) CreateServiceWithInternals()
    {
        DropTableService dropTableService = new();
        dropTableService.AddDropTable(DropTableId.TinBonusTable, new DropTable(
            new ItemReward(2, 5, ItemId.Tin),
            new TableReward(1, 1, DropTableId.CopperBonusTable)));
        dropTableService.AddDropTable(DropTableId.CopperBonusTable, new DropTable(
            new ItemReward(0, 10, ItemId.None),
            new ItemReward(1, 1, ItemId.Copper)));
        SocketRegistryService socketRegistry = new();
        ActivitySchedulerService scheduler = new();
        ActivityService service = new(_db.Factory, dropTableService, new ProfileService(_db.Factory, socketRegistry),
            new ItemService(_db.Factory), new SkillService(_db.Factory), socketRegistry, scheduler);
        return (service, socketRegistry, scheduler, dropTableService);
    }

    private (ActivityService, DropTableService) CreateService()
    {
        DropTableService dropTableService = new();
        dropTableService.AddDropTable(DropTableId.TinBonusTable, new DropTable(
            new ItemReward(2, 5, ItemId.Tin),
            new TableReward(1, 1, DropTableId.CopperBonusTable)));
        dropTableService.AddDropTable(DropTableId.CopperBonusTable, new DropTable(
            new ItemReward(0, 10, ItemId.None),
            new ItemReward(1, 1, ItemId.Copper)));
        SocketRegistryService socketRegistry = new();
        ActivityService service = new(_db.Factory, dropTableService, new ProfileService(_db.Factory, socketRegistry), new ItemService(_db.Factory), new SkillService(_db.Factory), socketRegistry, new ActivitySchedulerService());
        return (service, dropTableService);
    }

    private async Task<Profile> SeedProfileAsync()
    {
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Profile profile = new()
        {
            Name = $"P{Guid.NewGuid():N}"[..8],
            ProfileId = Guid.NewGuid(),
            CreationTime = DateTime.UtcNow,
            LastActiveTime = DateTime.UtcNow
        };
        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return profile;
    }

    private async Task SeedItemAsync(Profile profile, ItemId itemId, int count)
    {
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        dbContext.Profiles.Attach(profile);
        dbContext.Items.Add(new Item()
        {
            ProfileId = profile.ProfileId,
            Profile = profile,
            ItemId = itemId,
            Count = count,
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedSkillAsync(Profile profile, SkillId skillId, int xp, int level)
    {
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        dbContext.Profiles.Attach(profile);
        dbContext.Skills.Add(new Skill()
        {
            ProfileId = profile.ProfileId,
            Profile = profile,
            SkillId = skillId,
            Xp = xp,
            Level = level,
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<Item[]> GetItemsAsync(Guid profileId)
    {
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        return dbContext.Items
            .Where(i => i.ProfileId == profileId)
            .ToArray();
    }

    private async Task<Skill[]> GetSkillsAsync(Guid profileId)
    {
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        return dbContext.Skills
            .Where(s => s.ProfileId == profileId)
            .ToArray();
    }
}
