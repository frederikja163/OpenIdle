using System.Linq;
using Generator.Core.Spec;

namespace Generator.Core;

public sealed class AddEnumsVisitor : VisitorBase
{
    private readonly Enum _skillId = new("SkillId");
    private readonly Enum _itemTag = new("ItemTagId");
    private readonly Enum _itemId = new("ItemId");
    private readonly Enum _dropTableId = new("DropTableId");
    private readonly Enum _activityId = new("ActivityId");

    public override void Visit(Root root)
    {
        root.Enums.Add(_skillId);
        root.Enums.Add(_itemTag);
        root.Enums.Add(_itemId);
        root.Enums.Add(_dropTableId);
        root.Enums.Add(_activityId);
        root.Enums.Add(new Enum("ItemSlotId")
        {
            Values = root.Skills.SelectMany(s => s.Slots).Select(s => s.Name).Distinct()
                .Select(n => new EnumValue { Name = n }).ToList(),
        });

        foreach (Enum en in root.Enums)
        {
            if (en.Values.All(value => value.Name != "None"))
            {
                en.Values.Insert(0, new EnumValue { Name = "None" });
            }
        }
    }

    public override void Visit(Skill xmlSkill)
    {
        _skillId.AddValue(xmlSkill.Name);
    }

    public override void Visit(ItemTag xmlItemTag)
    {
        _itemTag.AddValue(xmlItemTag.Name);
    }

    public override void Visit(Item xmlItem)
    {
        _itemId.AddValue(xmlItem.Name);
    }

    public override void Visit(DropTable xmlDropTable)
    {
        _dropTableId.AddValue(xmlDropTable.Name);
    }

    public override void Visit(Activity xmlActivity)
    {
        _activityId.AddValue(xmlActivity.Name);
    }
}
