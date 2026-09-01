using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

namespace Backend.Services;

public sealed class ProfileService(IDbContextFactory<GameDbContext> dbContextFactory, SocketRegistryService socketRegistry)
{
    internal async Task<Profile[]> GetProfilesAsync(UserId userId)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Profiles
            .Where(p => p.Users.Any(u => u.UserId == userId))
            .ToArrayAsync();
    }

    internal async Task<Profile> GetProfileAsync(UserId userId, ProfileId profileId)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Profiles
                   .FirstOrDefaultAsync(p => p.ProfileId == profileId && p.Users.Any(u => u.UserId == userId))
               ?? throw new BackendException("Profile does not belong to user.");
    }

    internal async Task<Profile> GetProfileAsync(ProfileId profileId)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

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

        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        User user = new() { UserId = userId };
        Profile profile = new Profile()
        {
            ProfileId = Guid.NewGuid(),
            Name = name,
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
        socketRegistry.SetProfile(socket, profileId);
        return profile;
    }
}
