using System.Text.Json.Serialization;
using Backend.Dtos.Auth;

namespace Backend.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(PingRequest), nameof(PingRequest))]
[JsonDerivedType(typeof(PongResponse), nameof(PongResponse))]
[JsonDerivedType(typeof(CreateProfileRequest), nameof(CreateProfileRequest))]
[JsonDerivedType(typeof(CreateProfileResponse), nameof(CreateProfileResponse))]
[JsonDerivedType(typeof(ListProfilesRequest), nameof(ListProfilesRequest))]
[JsonDerivedType(typeof(ListProfilesResponse), nameof(ListProfilesResponse))]
[JsonDerivedType(typeof(LoginAsTestUserRequest), nameof(LoginAsTestUserRequest))]
[JsonDerivedType(typeof(LoginAsTestUserResponse), nameof(LoginAsTestUserResponse))]
[JsonDerivedType(typeof(SelectProfileRequest), nameof(SelectProfileRequest))]
[JsonDerivedType(typeof(SelectProfileResponse), nameof(SelectProfileResponse))]
public abstract class DtoBase
{
    
}

public abstract class RequestBase : DtoBase
{
    public int? Id { get; set; }
}

public abstract class ResponseBase : DtoBase
{
    public int? Id { get; set; }
}

public abstract class EventBase : DtoBase
{
    
}