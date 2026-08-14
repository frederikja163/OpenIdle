using System;
using System.Linq;
using System.Threading.Tasks;
using Backend;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;
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
        (ActivityService service, DropTableService dropTableService) = CreateService();
        dropTableService.AddDropTable(DropTableId.StoneTable, new DropTable(new ItemDrop(2, 5, ItemId.Stone)));
        service.AddActivity(ActivityId.Stone, DropTableId.StoneTable);

        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        Profile result = await service.StartActivityAsync(profile, ActivityId.Stone);
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
            () => service.StartActivityAsync(profile, ActivityId.Stone));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Does.Contain("valid drop table"));
        });
    }

    [Test]
    public async Task StartActivityAsync_NonexistentProfile_ThrowsBackendException()
    {
        (ActivityService service, DropTableService dropTableService) = CreateService();
        dropTableService.AddDropTable(DropTableId.StoneTable, new DropTable(new ItemDrop(2, 5, ItemId.Stone)));
        service.AddActivity(ActivityId.Stone, DropTableId.StoneTable);
        Profile profile = new()
        {
            Name = "Ghost",
            ProfileId = Guid.NewGuid(),
        };

        Assert.ThrowsAsync<BackendException>(() => service.StartActivityAsync(profile, ActivityId.Stone));
    }

    [Test]
    public async Task ResolveActivityAsync_AddsDroppedItemsToProfile()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, DropTableService dropTableService) = CreateService();
        dropTableService.AddDropTable(DropTableId.StoneTable, new DropTable(new ItemDrop(2, 5, ItemId.Stone)));
        service.AddActivity(ActivityId.Stone, DropTableId.StoneTable);
        await service.StartActivityAsync(profile, ActivityId.Stone);

        Item[] dropped = await service.ResolveActivityAsync(profile);

        Assert.Multiple(async () =>
        {
            Assert.That(dropped.Select(i => i.ItemId), Is.EqualTo(new[] { ItemId.Stone }));
            Assert.That(dropped.Select(i => i.Count), Is.EqualTo(new[] { 2 }));
            Item[] stored = await GetItemsAsync(profile);
            Assert.That(stored.Single().ItemId, Is.EqualTo(ItemId.Stone));
            Assert.That(stored.Single().Count, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_ExistingItemCountIsIncremented()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Stone, 1);
        (ActivityService service, DropTableService dropTableService) = CreateService();
        dropTableService.AddDropTable(DropTableId.StoneTable, new DropTable(new ItemDrop(2, 5, ItemId.Stone)));
        service.AddActivity(ActivityId.Stone, DropTableId.StoneTable);
        await service.StartActivityAsync(profile, ActivityId.Stone);

        await service.ResolveActivityAsync(profile);
        await service.ResolveActivityAsync(profile);

        Item stored = (await GetItemsAsync(profile)).Single();
        Assert.That(stored.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task ResolveActivityAsync_NoActivity_ThrowsBackendException()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, _) = CreateService();

        BackendException? exception = Assert.ThrowsAsync<BackendException>(() => service.ResolveActivityAsync(profile));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception?.Message, Is.EqualTo("Profile is not doing an activity."));
        });
    }

    [Test]
    public async Task ResolveActivityAsync_ZeroCountDrop_AddsNothing()
    {
        Profile profile = await SeedProfileAsync();
        (ActivityService service, DropTableService dropTableService) = CreateService();
        dropTableService.AddDropTable(DropTableId.BrokenRockTable, new DropTable(new ItemDrop(0, 10, ItemId.None)));
        service.AddActivity(ActivityId.Stone, DropTableId.BrokenRockTable);
        await service.StartActivityAsync(profile, ActivityId.Stone);

        Item[] dropped = await service.ResolveActivityAsync(profile);

        Assert.Multiple(async () =>
        {
            Assert.That(dropped, Is.Empty);
            Assert.That(await GetItemsAsync(profile), Is.Empty);
        });
    }

    [Test]
    public void AddActivity_DuplicateKey_ThrowsArgumentException()
    {
        (ActivityService service, _) = CreateService();
        service.AddActivity(ActivityId.Stone, DropTableId.StoneTable);

        Assert.Throws<ArgumentException>(() => service.AddActivity(ActivityId.Stone, DropTableId.StoneTable));
    }

    private (ActivityService, DropTableService) CreateService()
    {
        DropTableService dropTableService = new();
        return (new ActivityService(_db.Factory, dropTableService), dropTableService);
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

    private async Task<Item[]> GetItemsAsync(Profile profile)
    {
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        return dbContext.Items
            .Where(i => i.ProfileId == profile.ProfileId)
            .ToArray();
    }
}
