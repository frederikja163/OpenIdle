using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class Skill : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Slot")]
    public List<Slot> Slots { get; set; } = new();

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var s in Slots) s.Accept(visitor);
    }
}
