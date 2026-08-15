using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Database.Entities;

[Index(nameof(Name), IsUnique = true)]
public sealed class Profile
{
    [MaxLength(30)]
    public required string Name { get; init; }
    public required Guid ProfileId { get; init; }
    public ActivityId? ActivityId { get; set; }
    public DateTime? ActivityStartTime { get; set; }

    public ICollection<User> Users { get; } = [];
    public ICollection<Skill> Skills { get; } = [];
    public ICollection<Item> Items { get; } = [];

    public ProfileDto ToDto()
    {
        return new ProfileDto()
        {
            ProfileId = ProfileId,
            Name = Name,
        };
    }
}
