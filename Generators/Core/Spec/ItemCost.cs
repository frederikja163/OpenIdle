using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class ItemCost : INode
{
    [XmlAttribute("item")]
    public string Item { get; set; } = string.Empty;

    [XmlAttribute("cost")]
    public int Cost { get; set; }

    public void Accept(IVisitor visitor) => visitor.Visit(this);
}
