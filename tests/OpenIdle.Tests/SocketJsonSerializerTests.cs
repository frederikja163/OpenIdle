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
            Items = [new ItemDto() { ProfileId = Guid.NewGuid(), ItemId = ItemId.Tin, Count = 1 }],
        });

        Assert.That(json, Does.Contain("\"itemId\":\"Tin\""));
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
    public void Serialize_DefaultEnumValue_IsWritten()
    {
        // The generated TypeScript declares itemId required, so the frame has to
        // carry it even at its default.
        string json = Serialize(new GetItemsResponse()
        {
            Items = [new ItemDto() { ProfileId = Guid.NewGuid(), ItemId = ItemId.None, Count = 1 }],
        });

        Assert.That(json, Does.Contain("\"itemId\":\"None\""));
    }

    [Test]
    public void Serialize_ZeroValuedInt_IsWritten()
    {
        string json = Serialize(new GetSkillsResponse()
        {
            Skills = [new SkillDto() { ProfileId = Guid.NewGuid(), SkillId = SkillId.Mining, Xp = 0, Level = 1 }],
        });

        Assert.That(json, Does.Contain("\"xp\":0"));
    }

    [Test]
    public void Serialize_UnsetRequestId_IsOmitted()
    {
        // The frontend tells an event from a response by the absence of requestId,
        // so an unset `int?` id has to stay off the frame.
        string json = Serialize(new GetItemsResponse() { Items = [] });

        Assert.That(json, Does.Not.Contain("requestId"));
    }

    [Test]
    public void Serialize_ExplicitZeroRequestId_IsWritten()
    {
        // What Socket sends for a frame it could not read an id from.
        string json = Serialize(new ErrorResponse() { Message = "boom", RequestId = 0 });

        Assert.That(json, Does.Contain("\"requestId\":0"));
    }

    [Test]
    public void Deserialize_ItemIdEnum_RoundTrips()
    {
        byte[] bytes = SocketJsonSerializer.Serialize(new GetItemsResponse()
        {
            Items = [new ItemDto() { ProfileId = Guid.NewGuid(), ItemId = ItemId.Balsa, Count = 2 }],
        });

        DtoBase deserialized = SocketJsonSerializer.Deserialize(bytes, bytes.Length);

        Assert.That(deserialized, Is.InstanceOf<GetItemsResponse>());
        ItemDto item = ((GetItemsResponse)deserialized).Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.ItemId, Is.EqualTo(ItemId.Balsa));
            Assert.That(item.Count, Is.EqualTo(2));
        });
    }

    private static string Serialize(DtoBase dto)
    {
        return Encoding.UTF8.GetString(SocketJsonSerializer.Serialize(dto));
    }
}
