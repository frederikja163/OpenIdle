using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class DropTable : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("ItemReward")]
    public List<ItemReward> ItemRewards { get; set; } = new();

    [XmlElement("TableReward")]
    public List<TableReward> TableRewards { get; set; } = new();

    [XmlElement("XpReward")]
    public List<XpReward> XpRewards { get; set; } = new();

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var r in ItemRewards) r.Accept(visitor);
        foreach (var r in TableRewards) r.Accept(visitor);
        foreach (var r in XpRewards) r.Accept(visitor);
    }
}
