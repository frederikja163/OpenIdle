using System;
using Backend.Dtos;

namespace Backend.Database.Entities;

public sealed class ProfileSetting
{
    public required ProfileId ProfileId { get; init; }

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
