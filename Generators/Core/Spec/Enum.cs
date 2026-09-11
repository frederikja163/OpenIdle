using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class Enum() : INode
{
    public Enum(string name) : this()
    {
        Name = name;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Value")]
    public List<EnumValue> Values { get; set; } = new();

    public void AddValue(string value)
    {
        foreach (EnumValue existing in Values)
        {
            if (existing.Name == value)
            {
                return;
            }
        }

        Values.Add(new EnumValue { Name = value });
    }

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var v in Values) v.Accept(visitor);
    }
}
