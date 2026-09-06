using System;
using Backend.Database.Entities;
using Backend.Dtos;

namespace OpenIdle.Tests.Database.Entities;

[TestFixture]
public sealed class EntityToDtoTests
{
    [Test]
    public void Item_ToDto_MapsProperties()
    {
        Profile profile = new()
        {
            Name = "ItemOwner",
            ProfileId = Guid.NewGuid(),
            CreationTime = DateTime.UtcNow,
            LastActiveTime = DateTime.UtcNow
        };
        Item item = new()
        {
            ProfileId = profile.ProfileId,
            Profile = profile,
            ItemId = ItemId.Tin,
            Count = 7,
        };

        ItemDto dto = item.ToDto();

        Assert.Multiple(() =>
        {
            Assert.That(dto.ProfileId, Is.EqualTo(profile.ProfileId));
            Assert.That(dto.ItemId, Is.EqualTo(ItemId.Tin));
            Assert.That(dto.Count, Is.EqualTo(7));
        });
    }

    [Test]
    public void Skill_ToDto_MapsProperties()
    {
        Profile profile = new()
        {
            Name = "SkillOwner",
            ProfileId = Guid.NewGuid(),
            CreationTime = DateTime.UtcNow,
            LastActiveTime = DateTime.UtcNow
        };
        Skill skill = new()
        {
            ProfileId = profile.ProfileId,
            Profile = profile,
            SkillId = SkillId.Mining,
            Xp = 150,
            Level = 4,
        };

        SkillDto dto = skill.ToDto();

        Assert.Multiple(() =>
        {
            Assert.That(dto.ProfileId, Is.EqualTo(profile.ProfileId));
            Assert.That(dto.SkillId, Is.EqualTo(SkillId.Mining));
            Assert.That(dto.Xp, Is.EqualTo(150));
            Assert.That(dto.Level, Is.EqualTo(4));
        });
    }
}
