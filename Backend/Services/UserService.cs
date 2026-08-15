using System;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class UserService(IDbContextFactory<GameDbContext> dbContextFactory, SocketRegistryService socketRegistry)
{
    internal async Task<Guid> GetTestUserAsync()
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        User? user = await dbContext.Users.FirstOrDefaultAsync();
        if (user is null)
        {
            user = new User() { UserId = Guid.NewGuid() };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        return user.UserId;
    }

    internal void SignIn(Socket socket, Guid userId)
    {
        socket.UserId = userId;
        socketRegistry.SetUser(socket, userId);
    }
}
