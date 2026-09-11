using System.Collections.Generic;
using Generator.Core.Spec;

namespace Generator.Core;

/// <summary>
/// Validates the whole contract. All rules live here rather than on the spec nodes, so the
/// model stays data-only and the checks run in one place. Runs after
/// <see cref="AddEnumsVisitor"/> so the synthesised enums are already present.
/// </summary>
public sealed class ValidationVisitor : IVisitor
{
    private static readonly HashSet<string> BuiltInTypes = new()
    {
        "string", "int", "float", "guid", "userid", "profileid", "timestamp",
    };

    /// <summary>Keep in sync with <c>Backend.Services.ToolStat</c>, which the emitter references.</summary>
    private static readonly HashSet<string> KnownStats = new()
    {
        "Speed", "ItemProductivity", "XpProductivity", "Durable",
    };

    private readonly HashSet<string> _declaredTypes = new();

    public void Visit(Root root)
    {
        Guard.NotNull(root.Enums, "Enums");
        Guard.NotNull(root.DropTables, "DropTables");
        Guard.NotNull(root.Activities, "Activities");
        Guard.NotNull(root.Items, "Items");
        Guard.NotNull(root.Skills, "Skills");
        Guard.NotNull(root.Dtos, "Dtos");
        Guard.NotNull(root.Requests, "Requests");
        Guard.NotNull(root.Events, "Events");
        Guard.NotNull(root.Responses, "Responses");

        foreach (Response response in root.Responses)
        {
            Guard.Required(response.Name, "Top-level Response name");
        }

        foreach (Enum xmlEnum in root.Enums) _declaredTypes.Add(xmlEnum.Name);
        foreach (Dto dto in root.Dtos) _declaredTypes.Add(dto.Name);
    }

    public void Visit(Enum xmlEnum)
    {
        Guard.Required(xmlEnum.Name, "Enum name");
        Guard.NotNull(xmlEnum.Values, $"Enum '{xmlEnum.Name}' values");

        HashSet<string> seen = new();
        foreach (EnumValue value in xmlEnum.Values)
        {
            if (!seen.Add(value.Name))
            {
                throw new ParserException($"Enum '{xmlEnum.Name}' declares duplicate value '{value.Name}'.");
            }
        }
    }

    public void Visit(EnumValue xmlEnumValue) => Guard.Required(xmlEnumValue.Name, "Enum value name");

    public void Visit(DropTable xmlDropTable)
    {
        Guard.Required(xmlDropTable.Name, "DropTable name");
        Guard.NotNull(xmlDropTable.ItemRewards, $"DropTable '{xmlDropTable.Name}' item rewards");
        Guard.NotNull(xmlDropTable.TableRewards, $"DropTable '{xmlDropTable.Name}' table rewards");
        Guard.NotNull(xmlDropTable.XpRewards, $"DropTable '{xmlDropTable.Name}' xp rewards");

        foreach (Reward reward in Rewards(xmlDropTable))
        {
            if (reward.Weight is null)
            {
                throw new ParserException($"DropTable '{xmlDropTable.Name}' has a reward without a weight.");
            }
        }
    }

    public void Visit(Reward xmlReward) => ValidateReward(xmlReward);

    public void Visit(ItemReward xmlItemReward)
    {
        ValidateReward(xmlItemReward);
        Guard.Required(xmlItemReward.Item, "ItemReward item");
    }

    public void Visit(TableReward xmlTableReward)
    {
        ValidateReward(xmlTableReward);
        Guard.Required(xmlTableReward.Table, "TableReward table");
    }

    public void Visit(XpReward xmlXpReward)
    {
        ValidateReward(xmlXpReward);
        Guard.Required(xmlXpReward.Skill, "XpReward skill");
    }

    public void Visit(Activity xmlActivity)
    {
        Guard.Required(xmlActivity.Name, "Activity name");
        Guard.NotNull(xmlActivity.ItemRewards, $"Activity '{xmlActivity.Name}' item rewards");
        Guard.NotNull(xmlActivity.TableReward, $"Activity '{xmlActivity.Name}' table rewards");
        Guard.NotNull(xmlActivity.XpRewards, $"Activity '{xmlActivity.Name}' xp rewards");
        Guard.NotNull(xmlActivity.LevelRequirements, $"Activity '{xmlActivity.Name}' level requirements");
        Guard.NotNull(xmlActivity.ItemCosts, $"Activity '{xmlActivity.Name}' item costs");

        if (xmlActivity.Time <= 0)
        {
            throw new ParserException($"Activity '{xmlActivity.Name}' time must be greater than zero.");
        }
    }

    public void Visit(LevelRequirement xmlLevelRequirement)
    {
        Guard.Required(xmlLevelRequirement.Skill, "LevelRequirement skill");
        if (xmlLevelRequirement.Count < 0)
        {
            throw new ParserException($"LevelRequirement '{xmlLevelRequirement.Skill}' count must be non-negative.");
        }
    }

    public void Visit(ItemCost xmlItemCost)
    {
        Guard.Required(xmlItemCost.Item, "ItemCost item");
        if (xmlItemCost.Cost < 0)
        {
            throw new ParserException($"ItemCost '{xmlItemCost.Item}' cost must be non-negative.");
        }
    }

    public void Visit(Item xmlItem)
    {
        Guard.Required(xmlItem.Name, "Item name");
        Guard.NotNull(xmlItem.Tags, $"Item '{xmlItem.Name}' tags");
        Guard.NotNull(xmlItem.Stats, $"Item '{xmlItem.Name}' stats");
    }

    public void Visit(ItemTag xmlItemTag) => Guard.Required(xmlItemTag.Name, "ItemTag name");

    public void Visit(ItemStat xmlItemStat)
    {
        Guard.Required(xmlItemStat.Name, "ItemStat name");
        if (!KnownStats.Contains(xmlItemStat.Name))
        {
            throw new ParserException($"ItemStat '{xmlItemStat.Name}' is not a known tool stat.");
        }

        if (float.IsNaN(xmlItemStat.Value) || float.IsInfinity(xmlItemStat.Value))
        {
            throw new ParserException($"ItemStat '{xmlItemStat.Name}' value must be finite.");
        }
    }

    public void Visit(Skill xmlSkill)
    {
        Guard.Required(xmlSkill.Name, "Skill name");
        Guard.NotNull(xmlSkill.Slots, $"Skill '{xmlSkill.Name}' slots");
    }

    public void Visit(Slot xmlSlot)
    {
        Guard.Required(xmlSlot.Name, "Slot name");
        if (xmlSlot.AcceptedTag is null)
        {
            throw new ParserException($"Slot '{xmlSlot.Name}' must declare exactly one Tag.");
        }
    }

    public void Visit(Dto xmlDto)
    {
        Guard.Required(xmlDto.Name, "Dto name");
        Guard.NotNull(xmlDto.Properties, $"Dto '{xmlDto.Name}' properties");
    }

    public void Visit(Request xmlRequest)
    {
        Guard.Required(xmlRequest.Name, "Request name");
        Guard.NotNull(xmlRequest.Properties, $"Request '{xmlRequest.Name}' properties");
        Guard.NotNull(xmlRequest.Responses, $"Request '{xmlRequest.Name}' responses");

        if (xmlRequest.Responses.Count != 1)
        {
            throw new ParserException($"Request '{xmlRequest.Name}' must contain exactly one Response.");
        }
    }

    public void Visit(Response xmlResponse) => Guard.NotNull(xmlResponse.Properties, $"Response '{xmlResponse.Name}' properties");

    public void Visit(Event xmlEvent)
    {
        Guard.Required(xmlEvent.Name, "Event name");
        Guard.NotNull(xmlEvent.Properties, $"Event '{xmlEvent.Name}' properties");
    }

    public void Visit(Property xmlProperty)
    {
        Guard.Required(xmlProperty.Name, "Property name");
        Guard.Required(xmlProperty.Type, "Property type");

        if (!BuiltInTypes.Contains(xmlProperty.Type.ToLowerInvariant()) && !_declaredTypes.Contains(xmlProperty.Type))
        {
            throw new ParserException($"Property '{xmlProperty.Name}' has unknown type '{xmlProperty.Type}'.");
        }
    }

    private static void ValidateReward(Reward reward)
    {
        if (reward.Count < 0)
        {
            throw new ParserException("Reward count must be non-negative.");
        }

        if (reward.Weight is < 0)
        {
            throw new ParserException("Reward weight must be non-negative.");
        }
    }

    private static IEnumerable<Reward> Rewards(DropTable dropTable)
    {
        foreach (ItemReward reward in dropTable.ItemRewards) yield return reward;
        foreach (TableReward reward in dropTable.TableRewards) yield return reward;
        foreach (XpReward reward in dropTable.XpRewards) yield return reward;
    }
}
