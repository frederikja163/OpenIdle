using System;
using Backend.Dtos.Auth;

namespace Backend.Entities;

internal sealed class Profile
{
    public required string Name { get; init; }
    public required Guid ProfileId { get; init; }

    public ProfileDto ToDto()
    {
        return new ProfileDto()
        {
            ProfileId = ProfileId,
            Name = Name,
        };
    }
}