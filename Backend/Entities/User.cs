using System;
using System.Collections.Generic;
using Backend.Dtos.Auth;

namespace Backend.Entities;

internal sealed class User
{
    public required Guid UserId { get; init; }

    public UserDto ToDto()
    {
        return new UserDto()
        {
            UserId = UserId,
        };
    }
}