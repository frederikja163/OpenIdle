using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class TableReward : Reward
{
    [XmlAttribute("table")]
    public string Table { get; set; } = string.Empty;

    public override void Accept(IVisitor visitor) => visitor.Visit(this);
}
