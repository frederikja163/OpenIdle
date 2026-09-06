using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;
using OpenIdle.Tests.Database;
using NUnit.Framework;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class SettingsServiceTests : IDisposable
{
    private readonly TestGameDb _db = new();

    public void Dispose()
    {
        _db.Dispose();
    }

    [Test]
    public async Task SetUserSettings_CreatesNewSetting()
    {
        User user = await SeedUserAsync();
        var service = new SettingsService(_db.Factory);

        await service.SetUserSettings(user.UserId, new[]
        {
            new SettingDto { Name = "theme", Value = "dark" }
        });

        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        UserSetting stored = db.UserSettings.Single(s => s.UserId == user.UserId);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Name, Is.EqualTo("theme"));
            Assert.That(stored.Value, Is.EqualTo("dark"));
        });
    }

    [Test]
    public async Task SetUserSettings_UpdatesExistingSetting()
    {
        User user = await SeedUserAsync();
        await SeedUserSettingAsync(user, "theme", "light");
        var service = new SettingsService(_db.Factory);

        await service.SetUserSettings(user.UserId, new[]
        {
            new SettingDto { Name = "theme", Value = "dark" }
        });

        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        UserSetting stored = db.UserSettings.Single(s => s.UserId == user.UserId && s.Name == "theme");
        Assert.That(stored.Value, Is.EqualTo("dark"));
    }

    [Test]
    public async Task SetUserSettings_DoesNotAffectOtherUsers()
    {
        User user1 = await SeedUserAsync();
        User user2 = await SeedUserAsync();
        await SeedUserSettingAsync(user2, "theme", "light");
        var service = new SettingsService(_db.Factory);

        await service.SetUserSettings(user1.UserId, new[]
        {
            new SettingDto { Name = "theme", Value = "dark" }
        });

        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        UserSetting user1Setting = db.UserSettings.Single(s => s.UserId == user1.UserId);
        UserSetting user2Setting = db.UserSettings.Single(s => s.UserId == user2.UserId);
        Assert.Multiple(() =>
        {
            Assert.That(user1Setting.Value, Is.EqualTo("dark"));
            Assert.That(user2Setting.Value, Is.EqualTo("light"));
        });
    }

    [Test]
    public async Task GetUserSettings_ReturnsAllSettings()
    {
        User user = await SeedUserAsync();
        await SeedUserSettingAsync(user, "theme", "dark");
        await SeedUserSettingAsync(user, "notifications", "true");
        var service = new SettingsService(_db.Factory);

        SettingDto[] settings = await service.GetUserSettings(user.UserId);

        Assert.That(settings.Select(s => s.Name), Is.EquivalentTo(new[] { "theme", "notifications" }));
    }

    [Test]
    public async Task GetUserSettings_WithNames_FiltersRequestedSettings()
    {
        User user = await SeedUserAsync();
        await SeedUserSettingAsync(user, "theme", "dark");
        await SeedUserSettingAsync(user, "notifications", "true");
        await SeedUserSettingAsync(user, "sound", "100");
        var service = new SettingsService(_db.Factory);

        SettingDto[] settings = await service.GetUserSettings(user.UserId, new[] { "theme", "sound" });

        Assert.That(settings.Select(s => s.Name), Is.EquivalentTo(new[] { "theme", "sound" }));
    }

    [Test]
    public async Task SetProfileSettings_CreatesNewSetting()
    {
        Profile profile = await SeedProfileAsync();
        var service = new SettingsService(_db.Factory);

        await service.SetProfileSettings(profile.ProfileId, new[]
        {
            new SettingDto { Name = "auto_loot", Value = "true" }
        });

        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        ProfileSetting stored = db.ProfileSettings.Single(s => s.ProfileId == profile.ProfileId);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Name, Is.EqualTo("auto_loot"));
            Assert.That(stored.Value, Is.EqualTo("true"));
        });
    }

    [Test]
    public async Task SetProfileSettings_UpdatesExistingSetting()
    {
        Profile profile = await SeedProfileAsync();
        await SeedProfileSettingAsync(profile, "auto_loot", "false");
        var service = new SettingsService(_db.Factory);

        await service.SetProfileSettings(profile.ProfileId, new[]
        {
            new SettingDto { Name = "auto_loot", Value = "true" }
        });

        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        ProfileSetting stored = db.ProfileSettings.Single(s => s.ProfileId == profile.ProfileId && s.Name == "auto_loot");
        Assert.That(stored.Value, Is.EqualTo("true"));
    }

    [Test]
    public async Task SetProfileSettings_DoesNotAffectOtherProfiles()
    {
        Profile profile1 = await SeedProfileAsync();
        Profile profile2 = await SeedProfileAsync();
        await SeedProfileSettingAsync(profile2, "auto_loot", "false");
        var service = new SettingsService(_db.Factory);

        await service.SetProfileSettings(profile1.ProfileId, new[]
        {
            new SettingDto { Name = "auto_loot", Value = "true" }
        });

        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        ProfileSetting p1Setting = db.ProfileSettings.Single(s => s.ProfileId == profile1.ProfileId);
        ProfileSetting p2Setting = db.ProfileSettings.Single(s => s.ProfileId == profile2.ProfileId);
        Assert.Multiple(() =>
        {
            Assert.That(p1Setting.Value, Is.EqualTo("true"));
            Assert.That(p2Setting.Value, Is.EqualTo("false"));
        });
    }

    [Test]
    public async Task GetProfileSettings_ReturnsAllSettings()
    {
        Profile profile = await SeedProfileAsync();
        await SeedProfileSettingAsync(profile, "auto_loot", "true");
        await SeedProfileSettingAsync(profile, "show_timers", "false");
        var service = new SettingsService(_db.Factory);

        SettingDto[] settings = await service.GetProfileSettings(profile.ProfileId);

        Assert.That(settings.Select(s => s.Name), Is.EquivalentTo(new[] { "auto_loot", "show_timers" }));
    }

    private async Task<User> SeedUserAsync()
    {
        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        var user = new User { UserId = Guid.NewGuid() };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Profile> SeedProfileAsync()
    {
        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        var profile = new Profile
        {
            Name = $"P{Guid.NewGuid():N}"[..8],
            ProfileId = Guid.NewGuid()
        };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    private async Task SeedUserSettingAsync(User user, string name, string value)
    {
        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        db.Users.Attach(user);
        db.UserSettings.Add(new UserSetting
        {
            UserId = user.UserId,
            Name = name,
            Value = value
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedProfileSettingAsync(Profile profile, string name, string value)
    {
        await using GameDbContext db = await _db.Factory.CreateDbContextAsync();
        db.Profiles.Attach(profile);
        db.ProfileSettings.Add(new ProfileSetting
        {
            ProfileId = profile.ProfileId,
            Name = name,
            Value = value
        });
        await db.SaveChangesAsync();
    }
}
