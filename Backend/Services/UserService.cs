using System;
using Backend.Entities;

namespace Backend.Services;

public sealed class UserService
{
    private static User TestUser { get; } = new User() { UserId = Guid.NewGuid() }; // TODO: Remove in the future, here for testing.
    internal User GetTestUser()
    {
        return TestUser;
    }

    internal void SignIn(Socket socket, User user)
    {
        socket.User = user;
    }
}