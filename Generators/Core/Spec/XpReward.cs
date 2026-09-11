using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class XpReward : Reward
{
    [XmlAttribute("skill")]
    public string Skill { get; set; } = string.Empty;

    public override void Accept(IVisitor visitor) => visitor.Visit(this);
}
