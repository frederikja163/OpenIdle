using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Dtos;

namespace Backend;

internal static class SocketJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
    {
        AllowOutOfOrderMetadataProperties = true,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static byte[] Serialize(DtoBase dto)
    {
        return JsonSerializer.SerializeToUtf8Bytes(dto, typeof(DtoBase), Options);
    }

    internal static DtoBase Deserialize(byte[] bytes, int count)
    {
        return JsonSerializer.Deserialize<DtoBase>(bytes.AsSpan(0, count), Options)
               ?? throw new FormatException("Payload was either malformed json or an unrecognized json object.");
    }

    internal static RequestBase DeserializeRequest(byte[] bytes, int count)
    {
        return Deserialize(bytes, count) as RequestBase
               ?? throw new FormatException("Payload was either malformed json or an unrecognized json object.");
    }
}
