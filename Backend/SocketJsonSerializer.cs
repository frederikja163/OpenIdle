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
        // Null, not default: the nullable ids on the bases (requestId, eventId,
        // timestamp) stay off the frame while unset — the frontend tells an event
        // from a response by the absence of requestId — but every non-nullable
        // contract property is always written, because the generated TypeScript
        // declares it required. WhenWritingDefault would silently drop a genuine
        // 0 count or a `None` enum and hand the client `undefined` behind `number`.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
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
