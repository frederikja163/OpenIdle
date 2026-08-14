using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Generator.Core.Extensions;

namespace Generator.Core;

public sealed class ParserException(string message) : Exception(message);

public sealed class Parser
{
    private const string DropTableIdEnumName = "DropTableId";
    private const string ActivityIdEnumName = "ActivityId";

    public DtoModel Model { get; } = new();

    public void Parse(Stream stream)
    {
        XmlDocument document = new();
        document.Load(stream);

        XmlElement root = document.DocumentElement ?? throw new ParserException("Found no root node.");
        Parse(root);
    }

    public void Parse(XmlElement root)
    {
        foreach (XmlElement element in root.ChildNodes.OfType<XmlElement>())
        {
            Element(element);
        }
    }

    private void Element(XmlElement element)
    {
        switch (element.Name)
        {
            case "Enum":
                Enum en = Enum(element);
                Model.Enums.Add(en.Key, en);
                break;
            case "DropTable":
                DropTable dropTable = DropTable(element);
                Model.DropTables.Add(dropTable.Key, dropTable);
                GetEnum(DropTableIdEnumName).AddEnum(new EnumValue(dropTable.Key));
                break;
            case "Activity":
                Activity activity = Activity(element);
                Model.Activities.Add(activity.Key, activity);
                GetEnum(ActivityIdEnumName).AddEnum(new EnumValue(activity.Key));
                break;
            case "Dto":
                Dto dto = Dto(element);
                Model.Dtos.Add(dto.Key, dto);
                break;
            case "Request":
                Request request = Request(element);
                Model.Requests.Add(request.Key, request);
                Model.Responses.Add(request.Response.Key, request.Response);
                break;
            case "Event":
                Event ev = Event(element);
                Model.Events.Add(ev.Key, ev);
                break;
            case "Response":
                Response response = Response(element);
                Model.Responses.Add(response.Key, response);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Element name is not recognized '{element.Name}'", nameof(element.Name));
        }
    }

    private Enum GetEnum(string name)
    {
        if (!Model.Enums.TryGetValue(name, out Enum? en))
        {
            en = new Enum(name);
            Model.Enums[name] = en;
        }

        return en;
    }

    private Enum Enum(XmlElement element)
    {
        Enum en = new Enum(element.RequireAttribute("name"));
        foreach (XmlElement value in element.GetChildren("Value"))
        {
            en.AddEnum(EnumValue(value));
        }
        return en;
    }

    private EnumValue EnumValue(XmlElement element)
    {
        EnumValue enumValue = new EnumValue(element.RequireAttribute("name"));
        return enumValue;
    }

    private DropTable DropTable(XmlElement element)
    {
        DropTable dropTable = new DropTable(element.RequireAttribute("name"));
        foreach (XmlElement dropElement in element.GetChildren("Drop"))
        {
            dropTable.Drops.Add(Drop(dropElement));
        }
        return dropTable;
    }

    private Drop Drop(XmlElement element)
    {
        float weight = element.RequireAttribute<float>("weight");
        int count = element.RequireAttribute<int>("count");
        string item = element.GetAttribute("item");
        string table = element.GetAttribute("table");

        if (string.IsNullOrEmpty(item) == string.IsNullOrEmpty(table))
        {
            throw new ParserException($"Drop must specify exactly one of 'item' or 'table'.");
        }

        return new Drop(weight, count, string.IsNullOrEmpty(item) ? null : item, string.IsNullOrEmpty(table) ? null : table);
    }

    private Activity Activity(XmlElement element)
    {
        return new Activity(element.RequireAttribute("name"), element.RequireAttribute("table"));
    }

    private Dto Dto(XmlElement element)
    {
        Dto dto = new Dto(element.RequireAttribute("name"));
        dto.Properties.AddRange(PropertyCollection(element.GetChildren("Property")));
        return dto;
    }

    private Request Request(XmlElement element)
    {
        string baseName = element.RequireAttribute("name");
        List<XmlElement> responses = element.GetChildren("Response").ToList();
        if (responses.Count != 1)
        {
            throw new ParserException($"Request {baseName} does not have a valid response child tag.");
        }

        Request request = new Request(baseName, Response(responses[0], baseName));
        request.Properties.AddRange(PropertyCollection(element.GetChildren("Property")));
        return request;
    }

    private Response Response(XmlElement element, string? requestName = null)
    {
        string name = element.GetAttribute<string>("name", requestName)!;
        Response response = new Response(name);
        response.Properties.AddRange(PropertyCollection(element.GetChildren("Property")));
        return response;
    }

    private Event Event(XmlElement element)
    {
        Event value = new(element.RequireAttribute("name"));
        value.Properties.AddRange(PropertyCollection(element.GetChildren("Property")));
        return value;
    }

    private IEnumerable<Property> PropertyCollection(IEnumerable<XmlElement> nodeList)
    {
        foreach (XmlElement element in nodeList)
        {
            string propertyTypeStr = element.RequireAttribute("type");
            string name = element.RequireAttribute("name");
            bool multiple = element.GetAttribute<bool>("multiple", false);

            if (System.Enum.TryParse(propertyTypeStr, true, out PropertyType type))
            {
                yield return new Property(type, propertyTypeStr, name, multiple);
            }
            else if (Model.Dtos.TryGetValue(propertyTypeStr + "Dto", out Dto? dto))
            {
                yield return new CustomProperty(dto, name, multiple);
            }
            else if (Model.Enums.TryGetValue(propertyTypeStr, out Enum? en))
            {
                yield return new CustomProperty(en, name, multiple);
            }
            else
            {
                throw new ParserException($"Property '{name}' has an unknown type '{propertyTypeStr}'.");
            }
        }
    }
}
