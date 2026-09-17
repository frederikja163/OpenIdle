using System.Collections.Generic;
using System.Xml.Serialization;

namespace Generator.Core.Spec;

[XmlRoot("Types")]
public sealed class Root : INode
{
    [XmlElement("Enum")]
    public List<Enum> Enums { get; set; } = new();

    [XmlElement("DropTable")]
    public List<DropTable> DropTables { get; set; } = new();

    [XmlElement("Activity")]
    public List<Activity> Activities { get; set; } = new();

    [XmlElement("Item")]
    public List<Item> Items { get; set; } = new();

    [XmlElement("Skill")]
    public List<Skill> Skills { get; set; } = new();

    [XmlElement("Dto")]
    public List<Dto> Dtos { get; set; } = new();

    [XmlElement("Request")]
    public List<Request> Requests { get; set; } = new();

    [XmlElement("Event")]
    public List<Event> Events { get; set; } = new();

    [XmlElement("Response")]
    public List<Response> Responses { get; set; } = new();

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
}
