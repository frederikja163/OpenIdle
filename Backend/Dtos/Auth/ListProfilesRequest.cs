namespace Backend.Dtos.Auth;

public sealed class ListProfilesRequest : RequestBase
{
    
}

public sealed class ListProfilesResponse : ResponseBase
{
    public required ProfileDto[] Profiles { get; set; }
}