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
        await SeedItemAsync(profile, ItemId.Stone, 3);
        await SeedItemAsync(profile, ItemId.Wood, 5);
        Profile other = await SeedProfileAsync();
        await SeedItemAsync(other, ItemId.Food, 9);

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile);

        Assert.That(items.Select(i => i.ItemId), Is.EquivalentTo(new[] { ItemId.Stone, ItemId.Wood }));
    }

    [Test]
    public async Task GetItemsAsync_WithItemIds_FiltersToRequestedIds()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Stone, 1);
        await SeedItemAsync(profile, ItemId.Wood, 2);
        await SeedItemAsync(profile, ItemId.Food, 3);

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile, new[] { ItemId.Wood, ItemId.Food });

        Assert.That(items.Select(i => i.ItemId), Is.EquivalentTo(new[] { ItemId.Wood, ItemId.Food }));
    }

    [Test]
    public async Task GetItemsAsync_WithItemIds_DoesNotReturnItemsOfOtherProfile()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Stone, 1);
        Profile other = await SeedProfileAsync();
        await SeedItemAsync(other, ItemId.Wood, 2);

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile, new[] { ItemId.Stone, ItemId.Wood });

        Assert.That(items.Select(i => i.ItemId), Is.EqualTo(new[] { ItemId.Stone }));
    }

    [Test]
    public async Task GetItemsAsync_WithNoItems_ReturnsEmpty()
    {
        Profile profile = await SeedProfileAsync();

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile);

        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task GetItemsAsync_OrdersByItemId()
    {
        Profile profile = await SeedProfileAsync();
        await SeedItemAsync(profile, ItemId.Wood, 1);
        await SeedItemAsync(profile, ItemId.Food, 2);
        await SeedItemAsync(profile, ItemId.Stone, 3);

        ItemService service = new(_db.Factory);
        Item[] items = await service.GetItemsAsync(profile);

        Assert.That(items.Select(i => i.ItemId), Is.EqualTo(new[] { ItemId.Food, ItemId.Stone, ItemId.Wood }));
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
}
