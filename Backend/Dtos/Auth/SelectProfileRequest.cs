using System;

namespace Backend.Dtos.Auth;

public sealed class SelectProfileRequest : RequestBase
{
    public Guid ProfileId { get; set; }
}

public sealed class SelectProfileResponse : ResponseBase
{
}
