using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class Activity : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("time")]
    public float Time { get; set; }

    [XmlElement("ItemReward")]
    public List<ItemReward> ItemRewards { get; set; } = new();

    [XmlElement("TableReward")]
    public List<TableReward> TableReward { get; set; } = new();

    [XmlElement("XpReward")]
    public List<XpReward> XpRewards { get; set; } = new();

    [XmlElement("LevelRequirement")]
    public List<LevelRequirement> LevelRequirements { get; set; } = new();

    [XmlElement("ItemCost")]
    public List<ItemCost> ItemCosts { get; set; } = new();

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var r in ItemRewards) r.Accept(visitor);
        foreach (var r in TableReward) r.Accept(visitor);
        foreach (var r in XpRewards) r.Accept(visitor);
        foreach (var lr in LevelRequirements) lr.Accept(visitor);
        foreach (var ic in ItemCosts) ic.Accept(visitor);
    }
}
