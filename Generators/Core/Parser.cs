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
                throw new ParserException($"Element name is not recognized '{element.Name}'");
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
        foreach (XmlElement rewardElement in element.ChildNodes.OfType<XmlElement>())
        {
            dropTable.Rewards.Add(Reward(rewardElement, requireWeight: true));
        }
        return dropTable;
    }

    private Reward Reward(XmlElement element, bool requireWeight)
    {
        int count = element.RequireAttribute<int>("count");
        float? weight = element.HasAttribute("weight") ? element.GetAttribute<float>("weight") : null;

        if (requireWeight && weight is null)
        {
            throw new ParserException($"Reward in a drop table must specify a 'weight'.");
        }

        string targetAttribute = element.Name switch
        {
            "ItemReward" => "item",
            "TableReward" => "table",
            "XpReward" => "skill",
            _ => throw new ParserException($"Element name is not recognized '{element.Name}'"),
        };

        int specified = (element.HasAttribute("item") ? 1 : 0)
                        + (element.HasAttribute("table") ? 1 : 0)
                        + (element.HasAttribute("skill") ? 1 : 0);
        if (specified != 1)
        {
            throw new ParserException($"Reward must specify exactly one of 'item', 'table' or 'skill'.");
        }

        if (!element.HasAttribute(targetAttribute))
        {
            throw new ParserException($"Reward element '{element.Name}' must specify the matching '{targetAttribute}' attribute.");
        }

        string target = element.GetAttribute(targetAttribute);
        return element.Name switch
        {
            "ItemReward" => new ItemReward(weight, count, target),
            "TableReward" => new TableReward(weight, count, target),
            _ => new XpReward(weight, count, target),
        };
    }

    private Activity Activity(XmlElement element)
    {
        Activity activity = new Activity(element.RequireAttribute("name"));
        foreach (XmlElement rewardElement in element.ChildNodes.OfType<XmlElement>())
        {
            switch (rewardElement.Name)
            {
                case "ItemReward":
                case "TableReward":
                case "XpReward":
                    activity.Rewards.Add(Reward(rewardElement, requireWeight: false));
                    break;
                case "LevelRequirement":
                    activity.Requirements.Add(LevelRequirement(rewardElement));
                    break;
                default:
                    throw new ParserException($"Element name is not recognized '{rewardElement.Name}'");
            }
        }
        return activity;
    }

    private LevelRequirement LevelRequirement(XmlElement element)
    {
        return new LevelRequirement(element.RequireAttribute("skill"), element.RequireAttribute<int>("count"));
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
            bool optional = element.GetAttribute<bool>("optional", false);

            if (System.Enum.TryParse(propertyTypeStr, true, out PropertyType type))
            {
                if (type == PropertyType.Custom)
                {
                    throw new ParserException($"Property '{name}' has an unknown type '{propertyTypeStr}'");
                }
                yield return new Property(type, propertyTypeStr, name, multiple, optional);
            }
            else if (Model.Dtos.TryGetValue(propertyTypeStr + "Dto", out Dto? dto))
            {
                yield return new CustomProperty(dto, name, multiple, optional);
            }
            else if (Model.Enums.TryGetValue(propertyTypeStr, out Enum? en))
            {
                yield return new CustomProperty(en, name, multiple, optional);
            }
            else
            {
                throw new ParserException($"Property '{name}' has an unknown type '{propertyTypeStr}'.");
            }
        }
    }
}
