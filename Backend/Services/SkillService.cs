using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class SkillService(IDbContextFactory<GameDbContext> dbContextFactory)
{
    internal async Task<Skill[]> GetSkillsAsync(ProfileId profileId)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Dictionary<SkillId, Skill> skills = await dbContext.Skills
            .Where(s => s.ProfileId == profileId)
            .ToDictionaryAsync(s => s.SkillId);
        return Enum.GetValues<SkillId>()
            .Except([SkillId.None])
            .Select(s =>
                skills.TryGetValue(s, out Skill? skill) ? skill : new Skill { ProfileId = profileId, SkillId = s })
            .OrderBy(s => s.SkillId)
            .ToArray();
    }

    internal async Task<Skill[]> GetSkillsAsync(ProfileId profileId, ICollection<SkillId> skillIds)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Dictionary<SkillId, Skill> skills = await dbContext.Skills
            .Where(s => s.ProfileId == profileId && skillIds.Contains(s.SkillId))
            .ToDictionaryAsync(s => s.SkillId);
        return skillIds.Select(s =>
                skills.TryGetValue(s, out Skill? skill) ? skill : new Skill { ProfileId = profileId, SkillId = s })
            .OrderBy(s => s.SkillId)
            .ToArray();
    }

    internal async Task<Skill[]> AddSkillsAsync(ProfileId profileId, IEnumerable<XpReward> rewards)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Skill[] skills = await AddSkillsAsync(dbContext, profileId, rewards);
        await dbContext.SaveChangesAsync();
        return skills;
    }

    internal async Task<Skill[]> AddSkillsAsync(GameDbContext dbContext, ProfileId profileId, IEnumerable<XpReward> rewards)
    {
        List<Skill> skills = [];
        foreach (XpReward reward in rewards)
        {
            if (reward.Count <= 0)
            {
                continue;
            }

            Skill? skill = await dbContext.Skills
                .FirstOrDefaultAsync(s => s.ProfileId == profileId && s.SkillId == reward.SkillId);

            if (skill is null)
            {
                skill = new Skill()
                {
                    ProfileId = profileId,
                    SkillId = reward.SkillId,
                    Xp = reward.Count,
                    Level = 0,
                };
                dbContext.Skills.Add(skill);
            }
            else
            {
                skill.Xp += reward.Count;
            }

            skill.Level = LevelCurve.LevelFromXp(skill.Xp);
            skills.Add(skill);
        }

        return skills.ToArray();
    }
}
