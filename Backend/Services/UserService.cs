using System;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class UserService(IDbContextFactory<GameDbContext> dbContextFactory, SocketRegistryService socketRegistry)
{
    /// <summary>
    /// Subject reserved for the shared debug account. Chosen so it can never collide with a
    /// Zitadel <c>sub</c>, which is an opaque identifier issued by the identity provider.
    /// </summary>
    internal const string TestUserSubject = "openidle-test-user";

    internal async Task<User> GetTestUserAsync()
    {
        return await GetOrCreateBySubjectAsync(TestUserSubject);
    }

    internal async Task<User> GetOrCreateBySubjectAsync(string subject)
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        User? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Subject == subject);
        if (user is not null)
        {
            return user;
        }

        user = new User() { UserId = Guid.NewGuid(), Subject = subject };
        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException)
        {
            // A concurrent login created the same subject first; load theirs instead of failing.
            await using GameDbContext retryDbContext = await dbContextFactory.CreateDbContextAsync();
            User? existing = await retryDbContext.Users.FirstOrDefaultAsync(u => u.Subject == subject);
            if (existing is null)
            {
                throw;
            }

            return existing;
        }
    }

    internal void SignIn(Socket socket, UserId userId)
    {
        socket.UserId = userId;
        socketRegistry.SetUser(socket, userId);
    }
}
