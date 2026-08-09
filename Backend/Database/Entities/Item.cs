using System;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Database.Entities;

[Index(nameof(ProfileId), additionalPropertyNames: [nameof(SkillId)], IsUnique = true)]
public sealed class Item
{
    public Guid ProfileId { get; init; }
    public ItemId SkillId { get; init; }
    public int Count { get; init; }
}