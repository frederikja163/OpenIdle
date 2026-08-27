using System.IO;
using System.Text;
using Generator.Core;

namespace OpenIdle.Tests;

[TestFixture]
public sealed class ToolParserTests
{
    private static Parser Parse(string xml)
    {
        Parser parser = new();
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        using MemoryStream stream = new(bytes);
        parser.Parse(stream);
        return parser;
    }

    [Test]
    public void Parse_PopulatesItemStats()
    {
        Parser parser = Parse("""
            <Types>
              <Item name="IronPickaxeHead">
                <Stat name="speed" value="1.1"/>
                <Stat name="durable" value="1.5"/>
              </Item>
            </Types>
            """);

        Item item = parser.Model.Items["IronPickaxeHead"];
        Assert.That(item.Name.UpperCamelCase, Is.EqualTo("IronPickaxeHead"));
        Assert.That(item.Stats, Has.Count.EqualTo(2));
        Assert.That(item.Stats[0].Name, Is.EqualTo(ItemStats.Speed));
        Assert.That(item.Stats[0].Value, Is.EqualTo(1.1f));
        Assert.That(item.Stats[1].Name, Is.EqualTo(ItemStats.Durable));
        Assert.That(item.Stats[1].Value, Is.EqualTo(1.5f));
    }

    [Test]
    public void Parse_UnknownStat_Throws()
    {
        Parser parser = new();
        string xml = """
            <Types>
              <Item name="Rock">
                <Stat name="bogus" value="1.0"/>
              </Item>
            </Types>
            """;

        Assert.Throws<ParserException>(() => Parse(xml));
    }

    [Test]
    public void Parse_ItemRegistersIntoItemIdEnum()
    {
        Parser parser = Parse("""
            <Types>
              <Item name="OakHandle"/>
            </Types>
            """);

        Assert.That(parser.Model.Enums["ItemId"].GetEnum("OakHandle"), Is.Not.Null);
    }

    [Test]
    public void Parse_ItemSlotCollectsValidItems()
    {
        Parser parser = Parse("""
            <Types>
              <ItemSlot name="MiningHead">
                <ValidItem name="IronPickaxeHead"/>
                <ValidItem name="StoneHead"/>
              </ItemSlot>
            </Types>
            """);

        ItemSlot itemSlot = parser.Model.ItemSlots["MiningHead"];
        Assert.That(itemSlot.ValidItems, Has.Count.EqualTo(2));
        Assert.That(itemSlot.ValidItems[0].Name, Is.EqualTo("IronPickaxeHead"));
        Assert.That(parser.Model.Enums["ItemSlotId"].GetEnum("MiningHead"), Is.Not.Null);
    }

    [Test]
    public void Parse_SkillSlotsCollectsSlotBindings()
    {
        Parser parser = Parse("""
            <Types>
              <SkillSlots skill="Mining">
                <Slot name="MiningHead" required="true"/>
                <Slot name="Handle"/>
              </SkillSlots>
            </Types>
            """);

        Assert.That(parser.Model.SkillSlots, Has.Count.EqualTo(1));
        SkillSlots skillSlots = parser.Model.SkillSlots[0];
        Assert.That(skillSlots.Skill, Is.EqualTo("Mining"));
        Assert.That(skillSlots.Slots, Has.Count.EqualTo(2));
        Assert.That(skillSlots.Slots[0].Name, Is.EqualTo("MiningHead"));
        Assert.That(skillSlots.Slots[0].Required, Is.True);
        Assert.That(skillSlots.Slots[1].Required, Is.False);
    }
}
