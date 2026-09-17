using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class ItemTag : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    public void Accept(IVisitor visitor) => visitor.Visit(this);
}
