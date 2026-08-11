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
            case "Skill":
                Skill skill = Skill(element);
                Model.Skills.Add(skill.Key, skill);
                break;
            case "Item":
                Item item = Item(element);
                Model.Items.Add(item.Key, item);
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

    private Skill Skill(XmlElement element)
    {
        Skill skill = new Skill(element.RequireAttribute("name"));
        return skill;
    }

    private Item Item(XmlElement element)
    {
        Item item = new Item(element.RequireAttribute("name"));
        return item;
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
            if (!Enum.TryParse(propertyTypeStr, true, out PropertyType type))
            {
                type = PropertyType.Custom;
            }

            string name = element.RequireAttribute("name");
            bool multiple = element.GetAttribute<bool>("multiple", false);
            yield return new Property(type, propertyTypeStr, name, multiple);
        }
    }
}
