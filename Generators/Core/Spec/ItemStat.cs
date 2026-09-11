using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

public sealed class ItemStat : INode
{
    /// <summary>Keep in sync with <c>Backend.Services.ToolStat</c>, which the emitter references.</summary>
    private static readonly HashSet<string> Known = new()
    {
        "Speed",
        "ItemProductivity",
        "XpProductivity",
        "Durable",
    };

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("value")]
    public float Value { get; set; }

    public void Accept(IVisitor visitor) => visitor.Visit(this);
}
