using System.Diagnostics;
using Backend;
using Backend.Dtos;
using Backend.Services;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class DropTableServiceTests
{
    private static DropTableService CreateService()
    {
        return new DropTableService();
    }

    [Test]
    public void RollReward_SingleItemTable_ReturnsThatItem()
    {
        DropTableService service = CreateService();
        service.AddDropTable(DropTableId.StoneTable, new DropTable(new ItemReward(2, 5, ItemId.Stone)));

        Reward result = service.RollReward(DropTableId.StoneTable);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<ItemReward>());
            if (result is ItemReward itemReward)
            {
                Assert.That(itemReward.ItemId, Is.EqualTo(ItemId.Stone));
                Assert.That(itemReward.Count, Is.EqualTo(2));
                Assert.That(itemReward.Weight, Is.EqualTo(5f));
            }
        });
    }

    [Test]
    public void RollReward_UnknownDropTable_ThrowsBackendException()
    {
        DropTableService service = CreateService();

        Assert.Throws<BackendException>(() => service.RollReward(DropTableId.StoneTable));
    }

    [Test]
    public void RollReward_TableRewardToSingleItemTable_ReturnsNestedItem()
    {
        DropTableService service = CreateService();
        service.AddDropTable(DropTableId.StoneTable, new DropTable(new TableReward(1, 1, DropTableId.BrokenRockTable)));
        service.AddDropTable(DropTableId.BrokenRockTable, new DropTable(new ItemReward(1, 1, ItemId.BrokenRock)));

        Reward result = service.RollReward(DropTableId.StoneTable);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<ItemReward>());
            if (result is ItemReward itemReward)
            {
                Assert.That(itemReward.ItemId, Is.EqualTo(ItemId.BrokenRock));
            }
        });
    }

    [Test]
    public void RollReward_MultipleRewards_ReturnsOneOfTheAvailableRewards()
    {
        DropTableService service = CreateService();
        service.AddDropTable(DropTableId.BrokenRockTable, new DropTable(
            new ItemReward(0, 10, ItemId.None),
            new ItemReward(1, 1, ItemId.BrokenRock)));

        Reward result = service.RollReward(DropTableId.BrokenRockTable);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<ItemReward>());
            if (result is ItemReward itemReward)
            {
                Assert.That(itemReward.ItemId, Is.EqualTo(ItemId.None).Or.EqualTo(ItemId.BrokenRock));
            }
        });
    }

    [Test]
    public void RollReward_EmptyDropTable_ThrowsUnreachableException()
    {
        DropTableService service = CreateService();
        service.AddDropTable(DropTableId.BrokenRockTable, new DropTable());

        Assert.Throws<UnreachableException>(() => service.RollReward(DropTableId.BrokenRockTable));
    }

    [Test]
    public void DropTable_TotalWeight_SumsAllRewardWeights()
    {
        DropTable dropTable = new DropTable(new ItemReward(1, 2, ItemId.Stone), new ItemReward(1, 3, ItemId.Wood));

        Assert.Multiple(() =>
        {
            Assert.That(dropTable.TotalWeight, Is.EqualTo(5f));
            Assert.That(dropTable.Rewards, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public void AddDropTable_DuplicateKey_ThrowsArgumentException()
    {
        DropTableService service = CreateService();
        DropTable dropTable = new DropTable(new ItemReward(1, 1, ItemId.Stone));
        service.AddDropTable(DropTableId.StoneTable, dropTable);

        Assert.Throws<ArgumentException>(() => service.AddDropTable(DropTableId.StoneTable, dropTable));
    }
}
