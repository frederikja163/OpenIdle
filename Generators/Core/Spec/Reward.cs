using System.Xml.Serialization;

namespace Generator.Core.Spec;

public abstract class Reward : INode
{
    [XmlAttribute("count")]
    public int Count { get; set; }

    [XmlIgnore]
    public float? Weight { get; set; }

    [XmlAttribute("weight")]
    public string WeightProxy
    {
        get => Weight?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        set => Weight = string.IsNullOrEmpty(value) ? null : float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public abstract void Accept(IVisitor visitor);
}
