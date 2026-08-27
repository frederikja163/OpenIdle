using System;
using System.Collections.Generic;

namespace Backend.Database.Entities;

public sealed class User
{
    public required UserId UserId { get; init; }

    public ICollection<Profile> Profiles { get; } = [];
}
