using System.Linq;

namespace Generator.Core;

public sealed class AddEnumsVisitor : VisitorBase
{
    private readonly XmlEnum _skillId = new("SkillId");
    private readonly XmlEnum _itemTag = new("ItemTagId");
    private readonly XmlEnum _itemId = new("ItemId");
    private readonly XmlEnum _dropTableId = new("DropTableId");
    private readonly XmlEnum _activityId = new("ActivityId");

    public override void Visit(TypesXmlRoot root)
    {
        root.Enums.Add(_skillId);
        root.Enums.Add(_itemTag);
        root.Enums.Add(_itemId);
        root.Enums.Add(_dropTableId);
        root.Enums.Add(_activityId);
        root.Enums.Add(new XmlEnum("ItemSlotId")
        {
            Values = root.Skills.SelectMany(s => s.Slots).Select(s => s.Name).Distinct()
                .Select(n => new XmlEnumValue { Name = n }).ToList(),
        });

        foreach (XmlEnum en in root.Enums)
        {
            if (en.Values.All(value => value.Name != "None"))
            {
                en.Values.Insert(0, new XmlEnumValue { Name = "None" });
            }
        }
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

    public override void Visit(XmlDropTable xmlDropTable)
    {
        _dropTableId.AddValue(xmlDropTable.Name);
    }

    public override void Visit(XmlActivity xmlActivity)
    {
        _activityId.AddValue(xmlActivity.Name);
    }
}
