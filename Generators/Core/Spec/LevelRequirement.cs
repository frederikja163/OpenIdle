using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class LevelRequirement : INode
{
    [XmlAttribute("skill")]
    public string Skill { get; set; } = string.Empty;

    [XmlAttribute("count")]
    public int Count { get; set; }

    public void Accept(IVisitor visitor) => visitor.Visit(this);
}
