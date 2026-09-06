using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class SettingsService(IDbContextFactory<GameDbContext> dbContextFactory)
{
    public async Task SetUserSettings(UserId userId, SettingDto[] settings)
    {
        await using GameDbContext context = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        foreach (SettingDto setting in settings.GroupBy(s => s.Name, StringComparer.Ordinal).Select(g => g.Last()))
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "UserSettings" ("UserId", "Name", "Value")
                VALUES ({userId}, {setting.Name}, {setting.Value})
                ON CONFLICT ("UserId", "Name") DO UPDATE SET "Value" = excluded."Value";
                """);
        }
        await transaction.CommitAsync();
    }

    public async Task<SettingDto[]> GetUserSettings(UserId userId, string[] settingNames)
    {
        await using GameDbContext context = await dbContextFactory.CreateDbContextAsync();
        return context.UserSettings.Where(u => u.UserId == userId && settingNames.Contains(u.Name)).Select(u => u.ToDto()).ToArray();
    }

    public async Task<SettingDto[]> GetUserSettings(UserId userId)
    {
        await using GameDbContext context = await dbContextFactory.CreateDbContextAsync();
        return context.UserSettings.Where(u => u.UserId == userId).Select(u => u.ToDto()).ToArray();
    }

    public async Task SetProfileSettings(ProfileId profileId, SettingDto[] settings)
    {
        await using GameDbContext context = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        foreach (SettingDto setting in settings.GroupBy(s => s.Name, StringComparer.Ordinal).Select(g => g.Last()))
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ProfileSettings" ("ProfileId", "Name", "Value")
                VALUES ({profileId}, {setting.Name}, {setting.Value})
                ON CONFLICT ("ProfileId", "Name") DO UPDATE SET "Value" = excluded."Value";
                """);
        }
        await transaction.CommitAsync();
    }

    public async Task<SettingDto[]> GetProfileSettings(ProfileId profileId, string[] settingNames)
    {
        await using GameDbContext context = await dbContextFactory.CreateDbContextAsync();
        return context.ProfileSettings.Where(p => p.ProfileId == profileId && settingNames.Contains(p.Name)).Select(p => p.ToDto()).ToArray();
    }

    public async Task<SettingDto[]> GetProfileSettings(ProfileId profileId)
    {
        await using GameDbContext context = await dbContextFactory.CreateDbContextAsync();
        return context.ProfileSettings.Where(p => p.ProfileId == profileId).Select(p => p.ToDto()).ToArray();
    }
}
