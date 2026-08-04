using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

namespace Backend.Services;

public sealed class ProfileService(IDbContextFactory<GameDbContext> dbContextFactory)
{
    internal async Task<Profile[]> GetProfilesAsync(User user)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Profiles
            .Where(p => p.Users.Any(u => u.UserId == user.UserId))
            .ToArrayAsync();
    }

    internal async Task<Profile> GetProfileAsync(User user, Guid profileId)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Profiles
                   .FirstOrDefaultAsync(p => p.ProfileId == profileId && p.Users.Any(u => u.UserId == user.UserId))
               ?? throw new InvalidOperationException("Profile does not belong to user.");
    }

    internal async Task<Profile> CreateProfileAsync(User user, string name)
    {
        // TODO: Fix name duplication problem here.
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 30)
        {
            throw new ArgumentException("Profile name must be at most 30 characters.", nameof(name));
        }
        if (!name.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("Profile name must be alphanumeric.", nameof(name));
        }

        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        if (dbContext.Profiles.Any(p => p.Name == name))
        {
            throw new ArgumentException("Profile name is already taken.");
        }

        Profile profile = new Profile()
        {
            ProfileId = Guid.NewGuid(),
            Name = name,
        };

        dbContext.Attach(user);
        profile.Users.Add(user);
        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync();

        return profile;
    }

    internal async Task SelectProfileAsync(Socket socket, User user, Guid profileId)
    {
        socket.Profile = await GetProfileAsync(user, profileId);
    }
}
