using System;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class UserService(IDbContextFactory<GameDbContext> dbContextFactory)
{
    internal async Task<User> GetTestUserAsync()
    {
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        User? user = await dbContext.Users.FirstOrDefaultAsync();
        if (user is null)
        {
            user = new User() { UserId = Guid.NewGuid() };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        return user;
    }

    internal void SignIn(Socket socket, User user)
    {
        socket.User = user;
    }
}
