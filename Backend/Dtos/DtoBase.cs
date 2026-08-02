using System.Text.Json.Serialization;

namespace Backend.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(PingRequest), nameof(PingRequest))]
[JsonDerivedType(typeof(PongResponse), nameof(PongResponse))]
public abstract class DtoBase
{
    
}

public abstract class RequestBase : DtoBase
{
    
}

public abstract class ResponseBase : DtoBase
{

}

public abstract class EventBase : DtoBase
{
    
}