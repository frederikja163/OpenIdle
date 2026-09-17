using System.Text.Json;
using Generator.Core.Spec;

namespace Generator.Core;

public static class JsonSchemaEmitter
{
    public static string Emit(Root root) => JsonSerializer.Serialize(root);
}
