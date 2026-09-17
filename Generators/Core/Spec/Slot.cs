using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class Slot : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("required")]
    public bool Required { get; set; }

    [XmlElement("Tag")]
    public ItemTag AcceptedTag { get; set; } = null!;

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        AcceptedTag?.Accept(visitor);
    }
}
