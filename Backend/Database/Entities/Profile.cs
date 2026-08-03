using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Backend.Dtos.Auth;
using Microsoft.EntityFrameworkCore;

namespace Backend.Entities;

[Index(nameof(Name), IsUnique = true)]
public sealed class Profile
{
    [MaxLength(30)]
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
