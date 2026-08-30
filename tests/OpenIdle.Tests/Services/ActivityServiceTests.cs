using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdle.Tests.Database;

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
        AddStoneActivity(service);

        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        Profile result = await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);
        DateTime after = DateTime.UtcNow.AddSeconds(1);

        Assert.Multiple(() =>
        {
            Assert.That(result.ActivityId, Is.EqualTo(ActivityId.Stone));
            Assert.That(result.ActivityStartTime, Is.Not.Null);
            Assert.That(result.ActivityStartTime, Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(after));
        });
    }

    [Test]
    public async Task StartActivityAsync_UnknownActivity_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();

        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.StartActivityAsync(profile.ProfileId, ActivityId.Stone));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Does.Contain("valid definition"));
        });
    }

    [Test]
    public async Task StartActivityAsync_DoesNotMeetRequirement_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        AddStoneActivity(service);

        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.StartActivityAsync(profile.ProfileId, ActivityId.Stone));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Does.Contain("requires Mining level 1"));
        });
    }

    [Test]
    public async Task StartActivityAsync_NonexistentProfile_ThrowsBackendException()
    {
        (ActivityService service, _) = CreateService();
        AddStoneActivity(service);
        Profile profile = new()
        {
            Name = "Ghost",
            ProfileId = Guid.NewGuid(),
        };

        Assert.ThrowsAsync<BackendException>(() => service.StartActivityAsync(profile.ProfileId, ActivityId.Stone));
    }

    [Test]
    public async Task StartActivityAsync_AlreadyDoingActivity_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        (ActivityService service, _) = CreateService();
        AddStoneActivity(service);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);

        BackendException? exception = Assert.ThrowsAsync<BackendException>(
            () => service.StartActivityAsync(profile.ProfileId, ActivityId.Stone));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Does.Contain("already doing an activity"));
        });
    }

    [Test]
    public async Task StartActivityAsync_CompletionTimeElapsed_ResolvesRewardsAndClearsState()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 0, level: 1);
        SocketRegistryService socketRegistry = new(NullLogger<SocketRegistryService>.Instance);
        ActivitySchedulerService scheduler = new();
        ActivityService service = new(_db.Factory, new DropTableService(), new ProfileService(_db.Factory, socketRegistry),
            new ItemService(_db.Factory), new SkillService(_db.Factory), socketRegistry, scheduler);
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(
            time: 0.01f,
            rewards: [new ItemReward(2, null, ItemId.Stone)],
            requirements: []));

        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);
        Assert.That(await GetItemsAsync(profile.ProfileId), Is.Empty);

        Thread.Sleep(100);
        scheduler.NextEvent();

        Item[] items = await GetItemsAsync(profile.ProfileId);
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Profile? updated = await dbContext.Profiles.FindAsync(profile.ProfileId);
        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(1));
            Assert.That(items[0].ItemId, Is.EqualTo(ItemId.Stone));
            Assert.That(items[0].Count, Is.EqualTo(2));
            Assert.That(updated?.ActivityId, Is.Null);
        });
    }

    [Test]
    public async Task ResolveActivityAsync_NoActivity_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        AddStoneActivity(service);

        BackendException? exception = Assert.ThrowsAsync<BackendException>(() => service.ResolveActivityAsync(profile.ProfileId));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Is.EqualTo("Profile is not doing an activity."));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_GrantsGuaranteedItemAndXp()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Stone), new XpReward(10, null, SkillId.Mining)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);

        Reward[] rewards = await service.ResolveActivityAsync(profile.ProfileId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(rewards, Has.Length.EqualTo(2));
            ItemReward itemReward = rewards.OfType<ItemReward>().Single();
            Assert.That(itemReward.ItemId, Is.EqualTo(ItemId.Stone));
            Assert.That(itemReward.Count, Is.EqualTo(4));
            XpReward xpReward = rewards.OfType<XpReward>().Single();
            Assert.That(xpReward.SkillId, Is.EqualTo(SkillId.Mining));
            Assert.That(xpReward.Count, Is.EqualTo(10));
            Item item = (await GetItemsAsync(profile.ProfileId)).Single();
            Assert.That(item.ItemId, Is.EqualTo(ItemId.Stone));
            Assert.That(item.Count, Is.EqualTo(4));
            Skill skill = (await GetSkillsAsync(profile.ProfileId)).Single();
            Assert.That(skill.SkillId, Is.EqualTo(SkillId.Mining));
            Assert.That(skill.Xp, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_ExistingItemAndXpAreIncremented()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Stone, 1);
        await SeedSkillAsync(profile, SkillId.Mining, xp: 5, level: 1);
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Stone), new XpReward(10, null, SkillId.Mining)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);

        await service.ResolveActivityAsync(profile.ProfileId);
        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);
        await service.ResolveActivityAsync(profile.ProfileId);

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
        dropTableService.AddDropTable(DropTableId.StoneTable, new DropTable(new ItemReward(1, 1, ItemId.Stone)));
        SocketRegistryService socketRegistry = new(NullLogger<SocketRegistryService>.Instance);
        ActivityService service = new(_db.Factory, dropTableService, new ProfileService(_db.Factory, socketRegistry), new ItemService(_db.Factory), new SkillService(_db.Factory), socketRegistry, new ActivitySchedulerService());
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(
            time: 1f,
            rewards: [new TableReward(1, null, DropTableId.StoneTable)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);

        Reward[] rewards = await service.ResolveActivityAsync(profile.ProfileId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(rewards, Has.Length.EqualTo(1));
            Assert.That(rewards.Single(), Is.InstanceOf<ItemReward>());
            Item item = (await GetItemsAsync(profile.ProfileId)).Single();
            Assert.That(item.ItemId, Is.EqualTo(ItemId.Stone));
            Assert.That(item.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_WeightedRewards_RollsExactlyOne()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(2, 5, ItemId.Stone), new XpReward(10, 5, SkillId.Mining)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);

        Reward[] rewards = await service.ResolveActivityAsync(profile.ProfileId);

        Assert.Multiple(() =>
        {
            Assert.That(rewards, Has.Length.EqualTo(1));
            Assert.That(rewards.Single(), Is.InstanceOf<ItemReward>().Or.InstanceOf<XpReward>());
        });
    }

    [Test]
    public async Task ResolveActivityAsync_GuaranteedAndWeightedRewards_AllGranted()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Stone), new ItemReward(2, 5, ItemId.Wood), new XpReward(10, 5, SkillId.Mining)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);

        Reward[] rewards = await service.ResolveActivityAsync(profile.ProfileId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(rewards, Has.Length.EqualTo(2));
            ItemReward itemReward = rewards.OfType<ItemReward>().Single(r => r.ItemId == ItemId.Stone);
            Assert.That(itemReward.Count, Is.EqualTo(4));
            Item stone = (await GetItemsAsync(profile.ProfileId)).Single(i => i.ItemId == ItemId.Stone);
            Assert.That(stone.Count, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_ZeroCountItem_AddsNothing()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(0, null, ItemId.None)],
            requirements: []));
        await service.StartActivityAsync(profile.ProfileId, ActivityId.Stone);

        Reward[] rewards = await service.ResolveActivityAsync(profile.ProfileId);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(rewards, Is.Empty);
            Assert.That(await GetItemsAsync(profile.ProfileId), Is.Empty);
        });
    }

    [Test]
    public void AddActivity_DuplicateKey_ThrowsArgumentException()
    {
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(time: 1f, rewards: [], requirements: []));

        Assert.Throws<ArgumentException>(() =>
            service.AddActivity(ActivityId.Stone, new ActivityDefinition(time: 1f, rewards: [], requirements: [])));
    }

    private static void AddStoneActivity(ActivityService service)
    {
        service.AddActivity(ActivityId.Stone, new ActivityDefinition(
            time: 1f,
            rewards: [new ItemReward(4, null, ItemId.Stone), new XpReward(10, null, SkillId.Mining)],
            requirements: [new LevelRequirement(SkillId.Mining, 1)]));
    }

    private (ActivityService, DropTableService) CreateService()
    {
        DropTableService dropTableService = new();
        dropTableService.AddDropTable(DropTableId.StoneTable, new DropTable(
            new ItemReward(2, 5, ItemId.Stone),
            new TableReward(1, 1, DropTableId.BrokenRockTable)));
        dropTableService.AddDropTable(DropTableId.BrokenRockTable, new DropTable(
            new ItemReward(0, 10, ItemId.None),
            new ItemReward(1, 1, ItemId.BrokenRock)));
        SocketRegistryService socketRegistry = new(NullLogger<SocketRegistryService>.Instance);
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
