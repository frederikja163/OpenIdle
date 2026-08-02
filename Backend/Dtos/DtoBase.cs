using System.Text.Json.Serialization;

namespace Backend.Dtos;

[JsonPolymorphic]
[JsonDerivedType(typeof(PingRequest), nameof(PingRequest))]
[JsonDerivedType(typeof(PongResponse), nameof(PongResponse))]
public abstract class DtoBase
{
    
}