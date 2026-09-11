using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;
using OpenIdle.Tests.Database;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class ItemServiceTests : IDisposable
{
    private readonly TestGameDb _db = new();

    public void Dispose()
    {
        _db.Dispose();
    }

    [Test]
    public async Task GetItemsAsync_ReturnsOnlyItemsOfProfile()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Tin, 3);
        await SeedItemAsync(profile, ItemId.Balsa, 5);
        Profile other = await SeedProfileAsync();
        await SeedItemAsync(other, ItemId.Cedar, 9);

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile.ProfileId);

        Assert.That(items.Select(i => i.ItemId), Is.EquivalentTo(new[] { ItemId.Tin, ItemId.Balsa }));
    }

    [Test]
    public async Task GetItemsAsync_WithItemIds_FiltersToRequestedIds()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Tin, 1);
        await SeedItemAsync(profile, ItemId.Balsa, 2);
        await SeedItemAsync(profile, ItemId.Cedar, 3);

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile.ProfileId, new[] { ItemId.Balsa, ItemId.Cedar });

        Assert.That(items.Select(i => i.ItemId), Is.EquivalentTo(new[] { ItemId.Balsa, ItemId.Cedar }));
    }

    [Test]
    public async Task GetItemsAsync_WithItemIds_DoesNotReturnItemsOfOtherProfile()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Tin, 1);
        Profile other = await SeedProfileAsync();
        await SeedItemAsync(other, ItemId.Balsa, 2);

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile.ProfileId, new[] { ItemId.Tin, ItemId.Balsa });

        Assert.That(items.Select(i => i.ItemId), Is.EqualTo(new[] { ItemId.Tin }));
    }

    [Test]
    public async Task GetItemsAsync_WithNoItems_ReturnsEmpty()
    {
        Profile profile = await SeedProfileAsync();

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile.ProfileId);

        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task AddItemsAsync_AddsNewItem()
    {
        Profile profile = await SeedProfileAsync();

        ItemService service = new(_db.Factory);
        Item[] added = await service.AddItemsAsync(profile.ProfileId, new[] { new ItemReward(4, null, ItemId.Tin) });

        Assert.Multiple(() =>
        {
            Assert.That(added, Has.Length.EqualTo(1));
            Assert.That(added.Single().ItemId, Is.EqualTo(ItemId.Tin));
            Assert.That(added.Single().Count, Is.EqualTo(4));
        });
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Item stored = dbContext.Items.Single(i => i.ProfileId == profile.ProfileId);
        Assert.That(stored.Count, Is.EqualTo(4));
    }

    [Test]
    public async Task AddItemsAsync_IncrementsExistingItem()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Tin, 3);

        ItemService service = new(_db.Factory);
        await service.AddItemsAsync(profile.ProfileId, new[] { new ItemReward(4, null, ItemId.Tin) });

        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Item stored = dbContext.Items.Single(i => i.ProfileId == profile.ProfileId && i.ItemId == ItemId.Tin);
        Assert.That(stored.Count, Is.EqualTo(7));
    }

    [Test]
    public async Task AddItemsAsync_ZeroCountReward_AddsNothing()
    {
        Profile profile = await SeedProfileAsync();

        ItemService service = new(_db.Factory);
        Item[] added = await service.AddItemsAsync(profile.ProfileId, new[] { new ItemReward(0, null, ItemId.None) });

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.Empty);
        });
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Assert.That(dbContext.Items.Where(i => i.ProfileId == profile.ProfileId), Is.Empty);
    }

    [Test]
    public async Task AddItemsAsync_DuplicateItemInBatch_AggregatesCount()
    {
        Profile profile = await SeedProfileAsync();

        ItemService service = new(_db.Factory);
        Item[] added = await service.AddItemsAsync(profile.ProfileId,
            new[]
            {
                new ItemReward(4, null, ItemId.Tin),
                new ItemReward(3, null, ItemId.Tin),
            });

        Assert.Multiple(() =>
        {
            Assert.That(added, Has.Length.EqualTo(1));
            Assert.That(added.Single().ItemId, Is.EqualTo(ItemId.Tin));
        });
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Item stored = dbContext.Items.Single(i => i.ProfileId == profile.ProfileId && i.ItemId == ItemId.Tin);
        Assert.That(stored.Count, Is.EqualTo(7));
    }

    [Test]
    public async Task ApplyItemDeltaAsync_NetsRewardsAndCosts_SurvivesZeroInTheMiddle()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Cedar, 2);

        ItemService service = new(_db.Factory);
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Item[] items = await service.ApplyItemDeltaAsync(
            dbContext, profile.ProfileId,
            rewards: new[] { new ItemReward(3, null, ItemId.Cedar) },
            costs: new[] { new ItemCost(1, ItemId.Cedar) },
            completions: 3);
        await dbContext.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(1));
            Assert.That(items.Single().ItemId, Is.EqualTo(ItemId.Cedar));
        });
        Item stored = dbContext.Items.Single(i => i.ProfileId == profile.ProfileId && i.ItemId == ItemId.Cedar);
        Assert.That(stored.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetItemsAsync_OrdersByItemId()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Balsa, 1);
        await SeedItemAsync(profile, ItemId.Cedar, 2);
        await SeedItemAsync(profile, ItemId.Tin, 3);

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile.ProfileId);

        Assert.That(items.Select(i => i.ItemId), Is.EqualTo(new[] { ItemId.Balsa, ItemId.Cedar, ItemId.Tin }));
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
}
