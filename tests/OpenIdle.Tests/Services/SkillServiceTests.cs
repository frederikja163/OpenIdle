using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;
using OpenIdle.Tests.Database;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class SkillServiceTests : IDisposable
{
    private readonly TestGameDb _db = new();

    public void Dispose()
    {
        _db.Dispose();
    }

    [Test]
    public async Task GetSkillsAsync_ReturnsOnlySkillsOfProfile()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 100, level: 2);
        await SeedSkillAsync(profile, SkillId.Crafting, xp: 50, level: 1);
        Profile other = await SeedProfileAsync();
        await SeedSkillAsync(other, SkillId.LumberJacking, xp: 10, level: 1);

        SkillService service = new(_db.Factory);
        Skill[] skills = await service.GetSkillsAsync(profile);

        Assert.That(skills.Select(s => s.SkillId), Is.EquivalentTo(new[] { SkillId.Mining, SkillId.Crafting }));
    }

    [Test]
    public async Task GetSkillsAsync_WithSkillIds_FiltersToRequestedIds()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 100, level: 2);
        await SeedSkillAsync(profile, SkillId.Crafting, xp: 50, level: 1);
        await SeedSkillAsync(profile, SkillId.LumberJacking, xp: 10, level: 1);

        SkillService service = new(_db.Factory);
        Skill[] skills = await service.GetSkillsAsync(profile, new[] { SkillId.Mining, SkillId.LumberJacking });

        Assert.That(skills.Select(s => s.SkillId), Is.EquivalentTo(new[] { SkillId.Mining, SkillId.LumberJacking }));
    }

    [Test]
    public async Task GetSkillsAsync_WithNoSkills_ReturnsEmpty()
    {
        Profile profile = await SeedProfileAsync();

        SkillService service = new(_db.Factory);
        Skill[] skills = await service.GetSkillsAsync(profile);

        Assert.That(skills, Is.Empty);
    }

    [Test]
    public async Task GetSkillsAsync_OrdersBySkillId()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 100, level: 2);
        await SeedSkillAsync(profile, SkillId.LumberJacking, xp: 10, level: 1);
        await SeedSkillAsync(profile, SkillId.Crafting, xp: 50, level: 1);

        SkillService service = new(_db.Factory);
        Skill[] skills = await service.GetSkillsAsync(profile);

        Assert.That(skills.Select(s => s.SkillId), Is.EqualTo(new[] { SkillId.Crafting, SkillId.LumberJacking, SkillId.Mining }));
    }

    private async Task<Profile> SeedProfileAsync()
    {
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Profile profile = new()
        {
            Name = $"P{Guid.NewGuid():N}"[..8],
            ProfileId = Guid.NewGuid(),
        };
        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return profile;
    }

    private async Task SeedSkillAsync(Profile profile, SkillId skillId, int xp, int level)
    {
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        dbContext.Profiles.Attach(profile);
        dbContext.Skills.Add(new Skill()
        {
            ProfileId = profile.ProfileId,
            Profile = profile,
            SkillId = skillId,
            Xp = xp,
            Level = level,
        });
        await dbContext.SaveChangesAsync();
    }
}
