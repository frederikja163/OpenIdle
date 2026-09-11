using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core;

[XmlRoot("Types")]
public sealed class TypesXmlRoot : INode
{
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        Dtos.AcceptAll(visitor);
        Requests.AcceptAll(visitor);
        Events.AcceptAll(visitor);
        Responses.AcceptAll(visitor);
        Enums.AcceptAll(visitor);
        DropTables.AcceptAll(visitor);
        Activities.AcceptAll(visitor);
        Items.AcceptAll(visitor);
        Skills.AcceptAll(visitor);
    }
    [XmlElement("Enum")]
    public List<XmlEnum> Enums { get; set; } = new();

    [XmlElement("DropTable")]
    public List<XmlDropTable> DropTables { get; set; } = new();

    [XmlElement("Activity")]
    public List<XmlActivity> Activities { get; set; } = new();

    [XmlElement("Item")]
    public List<XmlItem> Items { get; set; } = new();

    [XmlElement("Skill")]
    public List<XmlSkill> Skills { get; set; } = new();

    [XmlElement("Dto")]
    public List<XmlDto> Dtos { get; set; } = new();

    [XmlElement("Request")]
    public List<XmlRequest> Requests { get; set; } = new();

    [XmlElement("Event")]
    public List<XmlEvent> Events { get; set; } = new();

    [XmlElement("Response")]
    public List<XmlResponse> Responses { get; set; } = new();
}

public sealed class XmlEnum() : INode
{
    public XmlEnum(string name) : this()
    {
        Name = name;
    }
    
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Value")]
    public List<XmlEnumValue> Values { get; set; } = new();

    public void AddValue(string value)
    {
        foreach (XmlEnumValue existing in Values)
        {
            if (existing.Name == value)
            {
                return;
            }
        }

        Values.Add(new XmlEnumValue { Name = value });
    }
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var v in Values) v.Accept(visitor);
    }
}

public sealed class XmlEnumValue : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    public void Accept(IVisitor visitor) => visitor.Visit(this);
}

public sealed class XmlDropTable : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("ItemReward")]
    public List<XmlItemReward> ItemRewards { get; set; } = new();

    [XmlElement("TableReward")]
    public List<XmlTableReward> TableRewards { get; set; } = new();

    [XmlElement("XpReward")]
    public List<XmlXpReward> XpRewards { get; set; } = new();
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var r in ItemRewards) r.Accept(visitor);
        foreach (var r in TableRewards) r.Accept(visitor);
        foreach (var r in XpRewards) r.Accept(visitor);
    }
}

public abstract class XmlReward : INode
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

public sealed class XmlItemReward : XmlReward
{
    [XmlAttribute("item")]
    public string Item { get; set; } = string.Empty;
    
    public override void Accept(IVisitor visitor) => visitor.Visit(this);
}

public sealed class XmlTableReward : XmlReward
{
    [XmlAttribute("table")]
    public string Table { get; set; } = string.Empty;
    
    public override void Accept(IVisitor visitor) => visitor.Visit(this);
}

public sealed class XmlXpReward : XmlReward
{
    [XmlAttribute("skill")]
    public string Skill { get; set; } = string.Empty;
    
    public override void Accept(IVisitor visitor) => visitor.Visit(this);
}

public sealed class XmlActivity : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("time")]
    public float Time { get; set; }

    [XmlElement("ItemReward")]
    public List<XmlItemReward> ItemRewards { get; set; } = new();

    [XmlElement("TableReward")]
    public List<XmlTableReward> TableReward { get; set; } = new();

    [XmlElement("XpReward")]
    public List<XmlXpReward> XpRewards { get; set; } = new();

    [XmlElement("LevelRequirement")]
    public List<XmlLevelRequirement> LevelRequirements { get; set; } = new();
    
    [XmlElement("ItemCost")]
    public List<XmlItemCost> ItemCosts { get; set; } = new();
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var r in ItemRewards) r.Accept(visitor);
        foreach (var r in TableReward) r.Accept(visitor);
        foreach (var r in XpRewards) r.Accept(visitor);
        foreach (var lr in LevelRequirements) lr.Accept(visitor);
        foreach (var ic in ItemCosts) ic.Accept(visitor);
    }
}

public sealed class XmlLevelRequirement : INode
{
    [XmlAttribute("skill")]
    public string Skill { get; set; } = string.Empty;

    [XmlAttribute("count")]
    public int Count { get; set; }
    
    public void Accept(IVisitor visitor) => visitor.Visit(this);
}

public sealed class XmlItemCost : INode
{
    [XmlAttribute("item")]
    public string Item { get; set; } = string.Empty;

    [XmlAttribute("cost")]
    public int Cost { get; set; }
    
    public void Accept(IVisitor visitor) => visitor.Visit(this);
}

public sealed class XmlItem : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Tag")]
    public List<XmlItemTag> Tags { get; set; } = new();

    [XmlElement("Stat")]
    public List<XmlItemStat> Stats { get; set; } = new();
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var t in Tags) t.Accept(visitor);
        foreach (var s in Stats) s.Accept(visitor);
    }
}

public sealed class XmlItemTag : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    public void Accept(IVisitor visitor) => visitor.Visit(this);
}

public sealed class XmlItemStat : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("value")]
    public float Value { get; set; }
    
    public void Accept(IVisitor visitor) => visitor.Visit(this);
}

public sealed class XmlSkill : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Slot")]
    public List<XmlSlot> Slots { get; set; } = new();
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var s in Slots) s.Accept(visitor);
    }
}

public sealed class XmlSlot : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("required")]
    public bool Required { get; set; }

    [XmlElement("Tag")]
    public XmlItemTag AcceptedTag { get; set; } = null!;
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        AcceptedTag?.Accept(visitor);
    }
}

public sealed class XmlDto : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Property")]
    public List<XmlProperty> Properties { get; set; } = new();
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var p in Properties) p.Accept(visitor);
    }
}

public sealed class XmlRequest : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Property")]
    public List<XmlProperty> Properties { get; set; } = new();

    [XmlElement("Response")]
    public XmlResponse Response { get; set; } = null!;
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var p in Properties) p.Accept(visitor);
        Response?.Accept(visitor);
    }
}

public sealed class XmlResponse : INode
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlElement("Property")]
    public List<XmlProperty> Properties { get; set; } = new();
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var p in Properties) p.Accept(visitor);
    }
}

public sealed class XmlEvent : INode
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Property")]
    public List<XmlProperty> Properties { get; set; } = new();
    
    public void Accept(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var p in Properties) p.Accept(visitor);
    }
}

public sealed class XmlProperty : INode
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