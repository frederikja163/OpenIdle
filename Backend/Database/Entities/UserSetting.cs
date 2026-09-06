using System;
using Backend.Dtos;

namespace Backend.Database.Entities;

public sealed class UserSetting
{
    public required UserId UserId { get; init; }

    public required string Name { get; init; }

    public required string Value { get; set; }

    public SettingDto ToDto()
    {
        return new SettingDto()
        {
            Name = Name,
            Value = Value,
        };
    }
}
