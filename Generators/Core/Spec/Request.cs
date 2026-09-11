using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class Request : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Property")]
    public List<Property> Properties { get; set; } = new();

    [XmlElement("Response")]
    public List<Response> Responses { get; set; } = new();

    /// <summary>
    /// The single response a request is answered with. Only valid once the contract has been
    /// validated; a request declares exactly one response.
    /// </summary>
    [XmlIgnore]
    [JsonIgnore]
    public Response Response => Responses.Single();

    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var p in Properties) p.Accept(visitor);
        foreach (var r in Responses) r.Accept(visitor);
    }
}
