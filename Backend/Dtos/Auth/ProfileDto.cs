using System;

namespace Backend.Dtos.Auth;

public sealed class ProfileDto
{
    public required string Name { get; set; }
    public required Guid ProfileId { get; set; }
}