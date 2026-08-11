using System;
using Backend.Dtos;

namespace Backend.Database.Entities;

public sealed class Skill
{
    public required Guid ProfileId { get; init; }
    public required Profile Profile { get; init; }
    public required SkillId SkillId { get; init; }
    public int Xp { get; set; }
    public int Level { get; set; }

    public SkillDto ToDto()
    {
        return new SkillDto()
        {
            ProfileId = ProfileId,
            SkillId = SkillId,
            Xp = Xp,
            Level = Level,
        };
    }
}
