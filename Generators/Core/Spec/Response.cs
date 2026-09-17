using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class Response : INode
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlElement("Property")]
    public List<Property> Properties { get; set; } = new();

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var p in Properties) p.Accept(visitor);
    }
}
