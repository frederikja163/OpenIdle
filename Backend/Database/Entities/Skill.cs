using System;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Database.Entities;

[Index(nameof(ProfileId), additionalPropertyNames: [nameof(SkillId)], IsUnique = true)]
public sealed class Skill
{
    public Guid ProfileId { get; init; }
    public SkillId SkillId { get; init; }
    public int Xp { get; init; }
    public int Level { get; init; }

    public SkillDto ToDto()
    {
        return new SkillDto()
        {
            ProfileId = ProfileId,
            SkillId = SkillId,
        }
    }
}