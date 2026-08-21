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
    internal async Task<Skill[]> GetSkillsAsync(Guid profileId)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Skills
            .Where(s => s.ProfileId == profileId)
            .OrderBy(s => s.SkillId)
            .ToArrayAsync();
    }

    internal async Task<Skill[]> GetSkillsAsync(Guid profileId, IEnumerable<SkillId> skillIds)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Skills
            .Where(s => s.ProfileId == profileId && skillIds.Contains(s.SkillId))
            .OrderBy(s => s.SkillId)
            .ToArrayAsync();
    }

    internal async Task<Skill[]> AddSkillsAsync(Guid profileId, IEnumerable<XpReward> rewards)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        Skill[] skills = await AddSkillsAsync(dbContext, profileId, rewards);
        await dbContext.SaveChangesAsync();
        return skills;
    }

    internal async Task<Skill[]> AddSkillsAsync(GameDbContext dbContext, Guid profileId, IEnumerable<XpReward> rewards)
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

            skills.Add(skill);
        }

        return skills.ToArray();
    }
}
