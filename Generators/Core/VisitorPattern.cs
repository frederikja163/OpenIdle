using System;
using System.Collections.Generic;

namespace Generator.Core;

public interface IVisitor
{
    public void Visit(TypesXmlRoot root);
    public void Visit(XmlEnum xmlEnum);
    public void Visit(XmlEnumValue xmlEnumValue);
    public void Visit(XmlDropTable xmlDropTable);
    public void Visit(XmlReward xmlReward);
    public void Visit(XmlItemReward xmlItemReward);
    public void Visit(XmlTableReward xmlTableReward);
    public void Visit(XmlXpReward xmlXpReward);
    public void Visit(XmlActivity xmlActivity);
    public void Visit(XmlLevelRequirement xmlLevelRequirement);
    public void Visit(XmlItemCost xmlItemCost);
    public void Visit(XmlItem xmlItem);
    public void Visit(XmlItemTag xmlItemTag);
    public void Visit(XmlItemStat xmlItemStat);
    public void Visit(XmlSkill xmlSkill);
    public void Visit(XmlSlot xmlSlot);
    public void Visit(XmlDto xmlDto);
    public void Visit(XmlRequest xmlRequest);
    public void Visit(XmlResponse xmlResponse);
    public void Visit(XmlEvent xmlEvent);
    public void Visit(XmlProperty xmlProperty);
}

public abstract class VisitorBase : IVisitor
{
    public virtual void Visit(TypesXmlRoot root){}
    public virtual void Visit(XmlEnum xmlEnum){}
    public virtual void Visit(XmlEnumValue xmlEnumValue){}
    public virtual void Visit(XmlDropTable xmlDropTable){}
    public virtual void Visit(XmlReward xmlReward){}
    public virtual void Visit(XmlItemReward xmlItemReward){}
    public virtual void Visit(XmlTableReward xmlTableReward){}
    public virtual void Visit(XmlXpReward xmlXpReward){}
    public virtual void Visit(XmlActivity xmlActivity){}
    public virtual void Visit(XmlLevelRequirement xmlLevelRequirement){}
    public virtual void Visit(XmlItemCost xmlItemCost){}
    public virtual void Visit(XmlItem xmlItem){}
    public virtual void Visit(XmlItemTag xmlItemTag){}
    public virtual void Visit(XmlItemStat xmlItemStat){}
    public virtual void Visit(XmlSkill xmlSkill){}
    public virtual void Visit(XmlSlot xmlSlot){}
    public virtual void Visit(XmlDto xmlDto){}
    public virtual void Visit(XmlRequest xmlRequest){}
    public virtual void Visit(XmlResponse xmlResponse){}
    public virtual void Visit(XmlEvent xmlEvent){}
    public virtual void Visit(XmlProperty xmlProperty){}
}

public interface INode
{
    void Accept(IVisitor visitor);
}
