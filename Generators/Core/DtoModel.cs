using System.Collections.Generic;
using System.Linq;

namespace Generator.Core;

public sealed class DtoModel
{
    public Dictionary<string, Dto> Dtos { get; } = [];
    public Dictionary<string, Event> Events { get; } = [];
    public Dictionary<string, Request> Requests { get; } = [];
    public Dictionary<string, Response> Responses { get; } = [];
    public Dictionary<string, Enum> Enums { get; } = [];
    public Dictionary<string, DropTable> DropTables { get; } = [];
    public Dictionary<string, Activity> Activities { get; } = [];

    public IEnumerable<Object> AllObjects => Dtos.Values.OfType<Object>().Union(Requests.Values).Union(Responses.Values)
        .Union(Events.Values);
}

public abstract class NamedType(string name)
{
    public Casing Name { get; } = new Casing(name);
    public string Key { get; } = name;   
}

public sealed class Enum : NamedType
{
    private readonly List<EnumValue> _values = [];
    private readonly Dictionary<string, EnumValue> _valuesByKey = [];

    public Enum(string name) : base(name)
    {
        AddEnum(new EnumValue("None"));
    }

    public void AddEnum(EnumValue value)
    {
        _values.Add(value);
        _valuesByKey.Add(value.Key, value);
    }

    public EnumValue? GetEnum(string key)
    {
        return _valuesByKey.TryGetValue(key, out EnumValue? value) ? value : null;
    }

    public IEnumerable<EnumValue> GetEnums()
    {
        return _values;
    }
}

public sealed class EnumValue(string name): NamedType(name)
{
}

public sealed class DropTable(string name) : NamedType(name)
{
    public List<Drop> Drops { get; } = [];
}

public sealed class Drop(float weight, int count, string? item, string? table)
{
    public float Weight { get; } = weight;
    public int Count { get; } = count;
    public string? Item { get; } = item;
    public string? Table { get; } = table;
}

public sealed class Activity(string name, string table) : NamedType(name)
{
    public string Table { get; } = table;
}

public enum PropertyType
{
    Custom,
    String,
    Int,
    Float,
    Guid,
    UserId,
    ProfileId,
}

public class Property(PropertyType type, string typeStr, string name, bool multiple)
{
    public Casing Name { get; } = new(name);
    public PropertyType PropertyType { get; } = type;
    public Casing PropertyTypeString { get; } = new(typeStr);
    public bool Multiple { get; } = multiple;
}

public sealed class CustomProperty(NamedType type, string name, bool multiple)
    : Property(PropertyType.Custom, type.Key, name, multiple)
{
    public NamedType Type { get; } = type;
}

public abstract class Object(string name) : NamedType(name)
{
    public List<Property> Properties { get; } = [];
}

public sealed class Dto(string name) : Object(name + "Dto")
{
}

public sealed class Request(string name, Response response) : Object(name + "Request")
{
    public Response Response { get; } = response;
}

public sealed class Response(string name) : Object(name + "Response")
{
}

public sealed class Event(string name) : Object(name + "Event");
