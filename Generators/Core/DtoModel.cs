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
    public Dictionary<string, Item> Items { get; } = [];
    public Dictionary<string, ItemSlot> ItemSlots { get; } = [];
    public List<SkillSlots> SkillSlots { get; } = [];

    public IEnumerable<Object> AllObjects => Dtos.Values.Union<Object>(Requests.Values)
        .Union<Object>(Responses.Values)
        .Union<Object>(Events.Values);
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
    public List<Reward> Rewards { get; } = [];
}

public abstract class Reward(float? weight, int count)
{
    public float? Weight { get; } = weight;
    public int Count { get; } = count;
}

public sealed class ItemReward(float? weight, int count, string item) : Reward(weight, count)
{
    public string Item { get; } = item;
}

public sealed class TableReward(float? weight, int count, string table) : Reward(weight, count)
{
    public string Table { get; } = table;
}

public sealed class XpReward(float? weight, int count, string skill) : Reward(weight, count)
{
    public string Skill { get; } = skill;
}

public sealed class LevelRequirement(string skill, int count)
{
    public string Skill { get; } = skill;
    public int Count { get; } = count;
}

public sealed class Activity(string name) : NamedType(name)
{
    public List<Reward> Rewards { get; } = [];
    public List<LevelRequirement> Requirements { get; } = [];
}

/// <summary>The stat names a declared item may carry. Keep in sync with the emitted <c>ToolStat</c> enum.</summary>
public static class ItemStats
{
    public const string Speed = "speed";
    public const string ItemProductivity = "itemProductivity";
    public const string XpProductivity = "xpProductivity";
    public const string Durable = "durable";

    public static readonly IReadOnlyDictionary<string, string> ByKey = new Dictionary<string, string>
    {
        [Speed] = "Speed",
        [ItemProductivity] = "ItemProductivity",
        [XpProductivity] = "XpProductivity",
        [Durable] = "Durable",
    };
}

public sealed class Item(string name) : NamedType(name)
{
    public List<ItemStat> Stats { get; } = [];
}

public sealed class ItemStat(string name, float value)
{
    public string Name { get; } = name;
    public float Value { get; } = value;
}

public sealed class ValidItem(string name)
{
    public string Name { get; } = name;
}

public sealed class ItemSlot(string name) : NamedType(name)
{
    public List<ValidItem> ValidItems { get; } = [];
}

public sealed class SkillSlots(string skill)
{
    public string Skill { get; } = skill;
    public List<Slot> Slots { get; } = [];
}

public sealed class Slot(string name, bool required)
{
    public string Name { get; } = name;
    public bool Required { get; } = required;
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

public class Property(PropertyType type, string typeStr, string name, bool multiple, bool optional)
{
    public Casing Name { get; } = new(name);
    public PropertyType PropertyType { get; } = type;
    public Casing PropertyTypeString { get; } = new(typeStr);
    public bool Multiple { get; } = multiple;
    public bool Optional { get; } = optional;
}

public sealed class CustomProperty(NamedType type, string name, bool multiple, bool optional)
    : Property(PropertyType.Custom, type.Key, name, multiple, optional)
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
