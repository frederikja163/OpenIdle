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
        Skill[] skills = await service.GetSkillsAsync(profile.ProfileId);

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
        Skill[] skills = await service.GetSkillsAsync(profile.ProfileId, new[] { SkillId.Mining, SkillId.LumberJacking });

        Assert.That(skills.Select(s => s.SkillId), Is.EquivalentTo(new[] { SkillId.Mining, SkillId.LumberJacking }));
    }

    [Test]
    public async Task GetSkillsAsync_WithNoSkills_ReturnsEmpty()
    {
        Profile profile = await SeedProfileAsync();

        SkillService service = new(_db.Factory);
        Skill[] skills = await service.GetSkillsAsync(profile.ProfileId);

        Assert.That(skills, Is.Empty);
    }

    [Test]
    public async Task AddSkillsAsync_AddsNewSkill()
    {
        Profile profile = await SeedProfileAsync();

        SkillService service = new(_db.Factory);
        Skill[] added = await service.AddSkillsAsync(profile.ProfileId, new[] { new XpReward(10, null, SkillId.Mining) });

        Assert.Multiple(() =>
        {
            Assert.That(added, Has.Length.EqualTo(1));
            Assert.That(added.Single().SkillId, Is.EqualTo(SkillId.Mining));
            Assert.That(added.Single().Xp, Is.EqualTo(10));
        });
        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Skill stored = dbContext.Skills.Single(s => s.ProfileId == profile.ProfileId);
        Assert.That(stored.Xp, Is.EqualTo(10));
    }

    [Test]
    public async Task AddSkillsAsync_IncrementsExistingSkillXp()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 5, level: 1);

        SkillService service = new(_db.Factory);
        await service.AddSkillsAsync(profile.ProfileId, new[] { new XpReward(10, null, SkillId.Mining) });

        await using GameDbContext dbContext = await _db.Factory.CreateDbContextAsync();
        Skill stored = dbContext.Skills.Single(s => s.ProfileId == profile.ProfileId && s.SkillId == SkillId.Mining);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Xp, Is.EqualTo(15));
            Assert.That(stored.Level, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetSkillsAsync_OrdersBySkillId()
    {
        Profile profile = await SeedProfileAsync();
        await SeedSkillAsync(profile, SkillId.Mining, xp: 100, level: 2);
        await SeedSkillAsync(profile, SkillId.LumberJacking, xp: 10, level: 1);
        await SeedSkillAsync(profile, SkillId.Crafting, xp: 50, level: 1);

        SkillService service = new(_db.Factory);
        Skill[] skills = await service.GetSkillsAsync(profile.ProfileId);

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
