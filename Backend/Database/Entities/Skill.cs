using System;
using Backend.Dtos;

namespace Backend.Database.Entities;

public sealed class Skill
{
    public required ProfileId ProfileId { get; init; }
    public Profile? Profile { get; init; }
    public required SkillId SkillId { get; init; }
    public int Xp { get; set; } = 0;
    public int Level { get; set; } = 1;

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
