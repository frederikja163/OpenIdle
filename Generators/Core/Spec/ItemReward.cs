using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class ItemReward : Reward
{
    [XmlAttribute("item")]
    public string Item { get; set; } = string.Empty;

    public override void Accept(IVisitor visitor) => visitor.Visit(this);
}
