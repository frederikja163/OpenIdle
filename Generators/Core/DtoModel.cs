using System.Collections.Generic;

namespace Generator.Core;

public sealed class DtoModel
{
    public List<Object> Objects { get; } = [];
}

public enum PropertyType
{
    Custom,
    String,
    Int,
    Float,
    Guid,
}

public sealed class Property(PropertyType type, string typeStr, string name, bool multiple)
{
    public Casing Name { get; } = new (name);
    public PropertyType PropertyType { get; } = type;
    public Casing PropertyTypeString { get; } = new Casing(typeStr);
    public bool Multiple { get; } = multiple;
}

public class Object(string name)
{
    public Casing Name { get; } = new Casing(name);
    public List<Property> Properties { get; } = [];
}

public sealed class Dto(string name): Object(name)
{
}

public sealed class Request(string name, Response response) : Object(name)
{
    public Response Response { get; } = response;
}

public sealed class Response(string name) : Object(name)
{
}

public sealed class Event(string name) : Object(name);
