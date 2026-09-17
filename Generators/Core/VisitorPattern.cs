using Generator.Core.Spec;

namespace Generator.Core;

public interface IVisitor
{
    public void Visit(Root root);
    public void Visit(Enum xmlEnum);
    public void Visit(EnumValue xmlEnumValue);
    public void Visit(DropTable xmlDropTable);
    public void Visit(Reward xmlReward);
    public void Visit(ItemReward xmlItemReward);
    public void Visit(TableReward xmlTableReward);
    public void Visit(XpReward xmlXpReward);
    public void Visit(Activity xmlActivity);
    public void Visit(LevelRequirement xmlLevelRequirement);
    public void Visit(ItemCost xmlItemCost);
    public void Visit(Item xmlItem);
    public void Visit(ItemTag xmlItemTag);
    public void Visit(ItemStat xmlItemStat);
    public void Visit(Skill xmlSkill);
    public void Visit(Slot xmlSlot);
    public void Visit(Dto xmlDto);
    public void Visit(Request xmlRequest);
    public void Visit(Response xmlResponse);
    public void Visit(Event xmlEvent);
    public void Visit(Property xmlProperty);
}

public abstract class VisitorBase : IVisitor
{
    public virtual void Visit(Root root){}
    public virtual void Visit(Enum xmlEnum){}
    public virtual void Visit(EnumValue xmlEnumValue){}
    public virtual void Visit(DropTable xmlDropTable){}
    public virtual void Visit(Reward xmlReward){}
    public virtual void Visit(ItemReward xmlItemReward){}
    public virtual void Visit(TableReward xmlTableReward){}
    public virtual void Visit(XpReward xmlXpReward){}
    public virtual void Visit(Activity xmlActivity){}
    public virtual void Visit(LevelRequirement xmlLevelRequirement){}
    public virtual void Visit(ItemCost xmlItemCost){}
    public virtual void Visit(Item xmlItem){}
    public virtual void Visit(ItemTag xmlItemTag){}
    public virtual void Visit(ItemStat xmlItemStat){}
    public virtual void Visit(Skill xmlSkill){}
    public virtual void Visit(Slot xmlSlot){}
    public virtual void Visit(Dto xmlDto){}
    public virtual void Visit(Request xmlRequest){}
    public virtual void Visit(Response xmlResponse){}
    public virtual void Visit(Event xmlEvent){}
    public virtual void Visit(Property xmlProperty){}
}

public interface INode
{
    void Accept(IVisitor visitor);
}

/// <summary>Runs each visitor over the whole contract, in order, before the next one starts.</summary>
public sealed class VisitorPipeline(params IVisitor[] visitors)
{
    public void Visit(Root root)
    {
        foreach (IVisitor visitor in visitors)
        {
            root.Accept(visitor);
        }
    }
}
