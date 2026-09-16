using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Backend.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

namespace Backend.Services;

public sealed class ProfileService
{
    private readonly IDbContextFactory<GameDbContext> _dbContextFactory;
    private readonly SocketRegistryService _socketRegistry;

    public ProfileService(IDbContextFactory<GameDbContext> dbContextFactory, SocketRegistryService socketRegistry)
    {
        _dbContextFactory = dbContextFactory;
        _socketRegistry = socketRegistry;
        _socketRegistry.ProfileOnline += SocketRegistryOnProfileOnline;
        _socketRegistry.ProfileOffline += SocketRegistryOnProfileOffline;
    }

    private async Task SocketRegistryOnProfileOnline(object? sender, ProfileOnlineEventArgs e)
    {
        await UpdateLastActiveAsync(e.ProfileId);
    }

    private async Task UpdateLastActiveAsync(ProfileId profileId)
    {
        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Profile profile = await dbContext.Profiles
                              .FirstOrDefaultAsync(p => p.ProfileId == profileId) ??
                          throw new UnreachableException("Profile does not exist.");
        profile.LastActiveTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    private async Task SocketRegistryOnProfileOffline(object? sender, ProfileOfflineEventArgs e)
    {
        await UpdateLastActiveAsync(e.ProfileId);
    }

    internal async Task<Profile[]> GetProfilesAsync(UserId userId)
    {
        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Profiles
            .Where(p => p.Users.Any(u => u.UserId == userId))
            .ToArrayAsync();
    }

    internal async Task<Profile> GetProfileAsync(UserId userId, ProfileId profileId)
    {
        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Profiles
                   .FirstOrDefaultAsync(p => p.ProfileId == profileId && p.Users.Any(u => u.UserId == userId))
               ?? throw new BackendException("Profile does not belong to user.");
    }

    internal async Task<Profile> GetProfileAsync(ProfileId profileId)
    {
        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Profiles
                   .FirstOrDefaultAsync(p => p.ProfileId == profileId)
               ?? throw new BackendException("Profile does not exist.");
    }

    internal async Task<Profile> CreateProfileAsync(UserId userId, string name)
    {
        BackendException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 30)
        {
            throw new BackendException("Profile name must be at most 30 characters.");
        }
        if (!name.All(char.IsAsciiLetterOrDigit))
        {
            throw new BackendException("Profile name must be alphanumeric.");
        }

        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        User user = new() { UserId = userId };
        Profile profile = new Profile()
        {
            ProfileId = Guid.NewGuid(),
            Name = name,
            CreationTime =  DateTime.UtcNow,
            LastActiveTime =  DateTime.UtcNow,
        };

        dbContext.Attach(user);
        profile.Users.Add(user);
        dbContext.Profiles.Add(profile);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueNameViolation(exception))
        {
            throw new BackendException("Profile name is already taken.");
        }

        return profile;
    }

    private static bool IsUniqueNameViolation(DbUpdateException exception)
    {
        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is SqliteException { SqliteExtendedErrorCode: raw.SQLITE_CONSTRAINT_UNIQUE })
            {
                return true;
            }
        }

        return false;
    }

    internal async Task<Profile> SelectProfileAsync(Socket socket, UserId userId, ProfileId profileId)
    {
        Profile profile = await GetProfileAsync(userId, profileId);
        socket.ProfileId = profileId;
        await _socketRegistry.SetProfile(socket, profileId);
        return profile;
    }

    public async Task SetActivityAsync(ProfileId profileId, ActivityId activityId, DateTime startTime)
    {
        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();
        await SetActivityAsync(dbContext, profileId, activityId, startTime);
        await dbContext.SaveChangesAsync();
    }

    internal async Task SetActivityAsync(GameDbContext dbContext, ProfileId profileId, ActivityId activityId, DateTime startTime)
    {
        Profile profile = await dbContext.Profiles.FirstOrDefaultAsync(p => p.ProfileId == profileId)
                          ?? throw new BackendException("Profile does not exist.");
        profile.ActivityId = activityId;
        profile.ActivityStartTime = startTime;
    }

    public async Task ClearActivityAsync(ProfileId profileId)
    {
        await using GameDbContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Profile profile = await dbContext.Profiles.FirstOrDefaultAsync(p => p.ProfileId == profileId)
                          ?? throw new BackendException("Profile does not exist.");
        profile.ActivityId = null;
        profile.ActivityStartTime = null;

        await dbContext.SaveChangesAsync();
    }
}
