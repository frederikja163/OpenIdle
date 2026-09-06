using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class SettingsService(IDbContextFactory<GameDbContext> dbContextFactory)
{
    public async Task SetUserSettings(UserId userId, SettingDto[] settings)
    {
        await using GameDbContext context = await dbContextFactory.CreateDbContextAsync();
        foreach (SettingDto setting in settings)
        {
            UserSetting? userSetting = await context.UserSettings.FirstOrDefaultAsync(u => u.UserId == userId && u.Name == setting.Name);
            if (userSetting is null)
            {
                userSetting = new UserSetting()
                {
                    UserId = userId,
                    Name = setting.Name,
                    Value = setting.Value
                };
                context.UserSettings.Add(userSetting);
            }
            userSetting.Value = setting.Value;
        }
        await context.SaveChangesAsync();
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
        foreach (SettingDto setting in settings)
        {
            ProfileSetting? profileSetting = await context.ProfileSettings.FirstOrDefaultAsync(p => p.ProfileId == profileId && p.Name == setting.Name);
            if (profileSetting is null)
            {
                profileSetting = new ProfileSetting()
                {
                    ProfileId = profileId,
                    Name = setting.Name,
                    Value = setting.Value
                };
                context.ProfileSettings.Add(profileSetting);
            }
            profileSetting.Value = setting.Value;
        }
        await context.SaveChangesAsync();
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
