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
    public void RollItem_SingleItemTable_ReturnsThatItem()
    {
        DropTableService service = CreateService();
        service.AddDropTable(DropTableId.StoneTable, new DropTable(new ItemDrop(2, 5, ItemId.Stone)));

        ItemDrop result = service.RollItem(DropTableId.StoneTable);

        Assert.Multiple(() =>
        {
            Assert.That(result.ItemId, Is.EqualTo(ItemId.Stone));
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.Weight, Is.EqualTo(5f));
        });
    }

    [Test]
    public void RollItem_UnknownDropTable_ThrowsBackendException()
    {
        DropTableService service = CreateService();

        Assert.Throws<BackendException>(() => service.RollItem(DropTableId.StoneTable));
    }

    [Test]
    public void RollItem_TableDropToSingleItemTable_ReturnsNestedItem()
    {
        DropTableService service = CreateService();
        service.AddDropTable(DropTableId.StoneTable, new DropTable(new TableDrop(1, 1, DropTableId.BrokenRockTable)));
        service.AddDropTable(DropTableId.BrokenRockTable, new DropTable(new ItemDrop(1, 1, ItemId.BrokenRock)));

        ItemDrop result = service.RollItem(DropTableId.StoneTable);

        Assert.That(result.ItemId, Is.EqualTo(ItemId.BrokenRock));
    }

    [Test]
    public void RollItem_MultipleDrops_ReturnsOneOfTheAvailableDrops()
    {
        DropTableService service = CreateService();
        service.AddDropTable(DropTableId.BrokenRockTable, new DropTable(
            new ItemDrop(0, 10, ItemId.None),
            new ItemDrop(1, 1, ItemId.BrokenRock)));

        ItemDrop result = service.RollItem(DropTableId.BrokenRockTable);

        Assert.That(result.ItemId, Is.EqualTo(ItemId.None).Or.EqualTo(ItemId.BrokenRock));
    }

    [Test]
    public void RollItem_EmptyDropTable_ThrowsUnreachableException()
    {
        DropTableService service = CreateService();
        service.AddDropTable(DropTableId.BrokenRockTable, new DropTable());

        Assert.Throws<UnreachableException>(() => service.RollItem(DropTableId.BrokenRockTable));
    }
    [Test]
    public void RollItem_UnknownActivity_ThrowsBackendException()
    {
        DropTableService service = CreateService();

        Assert.Throws<BackendException>(() => service.RollItem(DropTableId.StoneTable));
    }

    [Test]
    public void DropTable_TotalWeight_SumsAllDropWeights()
    {
        DropTable dropTable = new DropTable(new ItemDrop(1, 2, ItemId.Stone), new ItemDrop(1, 3, ItemId.Wood));

        Assert.Multiple(() =>
        {
            Assert.That(dropTable.TotalWeight, Is.EqualTo(5f));
            Assert.That(dropTable.Drops, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public void AddDropTable_DuplicateKey_ThrowsArgumentException()
    {
        DropTableService service = CreateService();
        DropTable dropTable = new DropTable(new ItemDrop(1, 1, ItemId.Stone));
        service.AddDropTable(DropTableId.StoneTable, dropTable);

        Assert.Throws<ArgumentException>(() => service.AddDropTable(DropTableId.StoneTable, dropTable));
    }
}
