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
    internal async Task<Skill[]> GetSkillsAsync(Profile profile)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Skills
            .Where(s => s.ProfileId == profile.ProfileId)
            .OrderBy(s => s.SkillId)
            .ToArrayAsync();
    }

    internal async Task<Skill[]> GetSkillsAsync(Profile profile, IEnumerable<SkillId> skillIds)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Skills
            .Where(s => s.ProfileId == profile.ProfileId && skillIds.Contains(s.SkillId))
            .OrderBy(s => s.SkillId)
            .ToArrayAsync();
    }
}
