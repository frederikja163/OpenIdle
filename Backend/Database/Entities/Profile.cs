using System;
using System.Collections.Generic;
using Backend.Dtos.Auth;

namespace Backend.Entities;

public sealed class Profile
{
    public required string Name { get; init; }
    public required Guid ProfileId { get; init; }

    public ICollection<User> Users { get; } = [];

    public ProfileDto ToDto()
    {
        return new ProfileDto()
        {
            ProfileId = ProfileId,
            Name = Name,
        };
    }
}
