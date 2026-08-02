namespace Backend.Dtos.Auth;


public sealed class CreateProfileRequest : RequestBase
{
    public required string Name { get; set; }
}

public sealed class CreateProfileResponse : ResponseBase
{
}