namespace Generator.Core;

public sealed class AddEnumsVisitor : VisitorBase
{
    private readonly XmlEnum _skillId = new("SkillId");
    private readonly XmlEnum _itemTag = new("ItemTag");
    private readonly XmlEnum _itemId = new("ItemId");
    private readonly XmlEnum _itemStat = new("ItemStat");
    private readonly XmlEnum _dropTableId = new("DropTableId");
    private readonly XmlEnum _activityId = new("ActivityId");

    public override void Visit(TypesXmlRoot root)
    {
        root.Enums.Add(_skillId);
        root.Enums.Add(_itemTag);
        root.Enums.Add(_itemId);
        root.Enums.Add(_itemStat);
        root.Enums.Add(_dropTableId);
        root.Enums.Add(_activityId);
    }

    public override void Visit(XmlSkill xmlSkill)
    {
        _skillId.AddValue(xmlSkill.Name);
    }
    
    public override void Visit(XmlItemTag xmlItemTag)
    {
        _itemTag.AddValue(xmlItemTag.Name);
    }

    public override void Visit(XmlItem xmlItem)
    {
        _itemId.AddValue(xmlItem.Name);
    }

    public override void Visit(XmlItemStat xmlItemStat)
    {
        _itemStat.AddValue(xmlItemStat.Name);
    }

    public override void Visit(XmlDropTable xmlDropTable)
    {
        _dropTableId.AddValue(xmlDropTable.Name);
    }

    public override void Visit(XmlActivity xmlActivity)
    {
        _activityId.AddValue(xmlActivity.Name);
    }
}