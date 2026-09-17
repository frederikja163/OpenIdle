using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class Property : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public string Type { get; set; } = string.Empty;

    [XmlAttribute("multiple")]
    public bool Multiple { get; set; }

    [XmlAttribute("optional")]
    public bool Optional { get; set; }

    public void Accept(IVisitor visitor) => visitor.Visit(this);
}
