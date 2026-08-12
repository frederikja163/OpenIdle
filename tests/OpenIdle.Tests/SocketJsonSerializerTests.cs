using System.Linq;
using System.Text;
using Backend;
using Backend.Dtos;

namespace OpenIdle.Tests;

[TestFixture]
public sealed class SocketJsonSerializerTests
{
    [Test]
    public void Serialize_ItemIdEnum_IsWrittenAsName()
    {
        string json = Serialize(new GetItemsResponse()
        {
            Items = [new ItemDto() { ProfileId = Guid.NewGuid(), ItemId = ItemId.Stone, Count = 1 }],
        });

        Assert.That(json, Does.Contain("\"itemId\":\"Stone\""));
    }

    [Test]
    public void Serialize_SkillIdEnum_IsWrittenAsName()
    {
        string json = Serialize(new GetSkillsResponse()
        {
            Skills = [new SkillDto() { ProfileId = Guid.NewGuid(), SkillId = SkillId.Mining, Xp = 10, Level = 1 }],
        });

        Assert.That(json, Does.Contain("\"skillId\":\"Mining\""));
    }

    [Test]
    public void Serialize_DefaultEnumValue_IsOmitted()
    {
        string json = Serialize(new GetItemsResponse()
        {
            Items = [new ItemDto() { ProfileId = Guid.NewGuid(), ItemId = ItemId.None, Count = 1 }],
        });

        Assert.That(json, Does.Not.Contain("itemId"));
    }

    [Test]
    public void Deserialize_ItemIdEnum_RoundTrips()
    {
        byte[] bytes = SocketJsonSerializer.Serialize(new GetItemsResponse()
        {
            Items = [new ItemDto() { ProfileId = Guid.NewGuid(), ItemId = ItemId.Wood, Count = 2 }],
        });

        DtoBase deserialized = SocketJsonSerializer.Deserialize(bytes, bytes.Length);

        Assert.That(deserialized, Is.InstanceOf<GetItemsResponse>());
        ItemDto item = ((GetItemsResponse)deserialized).Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.ItemId, Is.EqualTo(ItemId.Wood));
            Assert.That(item.Count, Is.EqualTo(2));
        });
    }

    private static string Serialize(DtoBase dto)
    {
        return Encoding.UTF8.GetString(SocketJsonSerializer.Serialize(dto));
    }
}
