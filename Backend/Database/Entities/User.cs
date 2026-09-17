using System;
using System.Collections.Generic;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Database.Entities;

[Index(nameof(Subject), IsUnique = true)]
public sealed class User
{
    public required UserId UserId { get; init; }

    /// <summary>
    /// The token's <c>sub</c> claim, unique per issuer. This is the external identity the
    /// account is keyed by; never an email or any other unverified claim.
    /// </summary>
    public required string Subject { get; init; }

    public ICollection<Profile> Profiles { get; } = [];

    public UserDto ToDto()
    {
        return new UserDto()
        {
            UserId = UserId,
        };
    }
}
