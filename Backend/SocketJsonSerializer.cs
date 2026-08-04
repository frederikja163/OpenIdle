using System;
using System.Text;
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
        string json = JsonSerializer.Serialize(dto, typeof(DtoBase), Options);
        return Encoding.UTF8.GetBytes(json);
    }

    internal static RequestBase DeserializeRequest(byte[] bytes, int count)
    {
        string json = Encoding.UTF8.GetString(bytes, 0, count);
        return JsonSerializer.Deserialize<DtoBase>(json, Options) as RequestBase
               ?? throw new FormatException(
                   "Payload was either malformed json or an unrecognized json object.");
    }
}
