using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class Item : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Tag")]
    public List<ItemTag> Tags { get; set; } = new();

    [XmlElement("Stat")]
    public List<ItemStat> Stats { get; set; } = new();

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var t in Tags) t.Accept(visitor);
        foreach (var s in Stats) s.Accept(visitor);
    }
}
