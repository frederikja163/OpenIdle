using System.Text.Json;

namespace Generator.Core;

public static class JsonSchemaEmitter
{
    public static string Emit(TypesXmlRoot root) => JsonSerializer.Serialize(root);
}
