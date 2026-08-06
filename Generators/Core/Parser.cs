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
            case "Dto":
                Model.Objects.Add(Dto(element));
                break;
            case "Request":
                Request request = Request(element);
                Model.Objects.Add(request.Response);
                Model.Objects.Add(request);
                break;
            case "Event":
                Model.Objects.Add(Event(element));
                break;
            case "Response":
                Model.Objects.Add(Response(element));
                break;
        }
    }

    private Dto Dto(XmlElement element)
    {
        Dto dto = new Dto(element.RequireAttribute("name") + "Dto");
        dto.Properties.AddRange(PropertyCollection(element.GetChildren("Property")));
        return dto;
    }

    private Request Request(XmlElement element)
    {
        string baseName = element.RequireAttribute("name");
        XmlNodeList responses = element.GetElementsByTagName("Response");
        if (responses.Count != 1 || responses[0] is not XmlElement responseElement)
        {
            throw new ParserException($"Request {baseName} does not have a valid response child tag.");
        }

        Request request = new Request(baseName + "Request", Response(responseElement, baseName));
        request.Properties.AddRange(PropertyCollection(element.GetChildren("Property")));
        return request;
    }

    private Response Response(XmlElement element, string? requestName = null)
    {
        string name = element.GetAttribute<string>("name", requestName)!;
        Response response = new Response(name + "Response");
        response.Properties.AddRange(PropertyCollection(element.GetChildren("Property")));
        return response;
    }

    private Event Event(XmlElement element)
    {
        Event value = new(element.RequireAttribute("name") + "Event");
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
