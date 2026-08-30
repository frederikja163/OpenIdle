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
    public void Parse_PopulatesItemStatsAndOrderedTags()
    {
        Parser parser = Parse("""
            <Types>
              <Item name="IronPickaxeHead">
                <Tag name="head"/>
                <Tag name="iron"/>
                <Stat name="speed" value="1.1"/>
                <Stat name="durable" value="1.5"/>
              </Item>
            </Types>
            """);

        Item item = parser.Model.Items["IronPickaxeHead"];
        Assert.That(item.Name.UpperCamelCase, Is.EqualTo("IronPickaxeHead"));
        Assert.That(item.Tags, Has.Count.EqualTo(2));
        Assert.That(item.Tags[0].Name, Is.EqualTo("head"));
        Assert.That(item.Tags[1].Name, Is.EqualTo("iron"));
        Assert.That(item.Stats, Has.Count.EqualTo(2));
        Assert.That(item.Stats[0].Name, Is.EqualTo(ItemStats.Speed));
        Assert.That(item.Stats[0].Value, Is.EqualTo(1.1f));
        Assert.That(item.Stats[1].Name, Is.EqualTo(ItemStats.Durable));
        Assert.That(item.Stats[1].Value, Is.EqualTo(1.5f));
    }

    [Test]
    public void Parse_TagsRegisterIntoItemTagIdEnumDeduplicated()
    {
        Parser parser = Parse("""
            <Types>
              <Item name="Stone">
                <Tag name="ore"/>
              </Item>
              <Item name="BrokenRock">
                <Tag name="ore"/>
              </Item>
              <Item name="IronPickaxeHead">
                <Tag name="head"/>
                <Tag name="iron"/>
              </Item>
            </Types>
            """);

        Generator.Core.Enum tagEnum = parser.Model.Enums["ItemTagId"];
        Assert.That(tagEnum.GetEnum("ore"), Is.Not.Null);
        Assert.That(tagEnum.GetEnum("head"), Is.Not.Null);
        Assert.That(tagEnum.GetEnum("iron"), Is.Not.Null);
        Assert.That(parser.Model.Items["Stone"].Tags[0].Name, Is.EqualTo("ore"));
        Assert.That(parser.Model.Items["BrokenRock"].Tags[0].Name, Is.EqualTo("ore"));
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
    public void Parse_SkillSlotsCollectsSlotBindings()
    {
        Parser parser = Parse("""
            <Types>
              <SkillSlots skill="Mining">
                <Slot name="Head" required="true">
                  <Tag name="head"/>
                </Slot>
                <Slot name="Handle">
                  <Tag name="handle"/>
                </Slot>
              </SkillSlots>
            </Types>
            """);

        Assert.That(parser.Model.SkillSlots, Has.Count.EqualTo(1));
        SkillSlots skillSlots = parser.Model.SkillSlots[0];
        Assert.That(skillSlots.Skill, Is.EqualTo("Mining"));
        Assert.That(skillSlots.Slots, Has.Count.EqualTo(2));
        Assert.That(skillSlots.Slots[0].Name, Is.EqualTo("Head"));
        Assert.That(skillSlots.Slots[0].Tag.Name, Is.EqualTo("head"));
        Assert.That(skillSlots.Slots[0].Required, Is.True);
        Assert.That(skillSlots.Slots[1].Tag.Name, Is.EqualTo("handle"));
        Assert.That(skillSlots.Slots[1].Required, Is.False);
    }

    [Test]
    public void Parse_ItemSlotRegistersIntoItemSlotIdEnum()
    {
        Parser parser = Parse("""
            <Types>
              <SkillSlots skill="Mining">
                <Slot name="Head" required="true">
                  <Tag name="head"/>
                </Slot>
              </SkillSlots>
            </Types>
            """);

        Assert.That(parser.Model.Enums["ItemSlotId"].GetEnum("Head"), Is.Not.Null);
    }

    [Test]
    public void Parse_SlotWithoutTag_Throws()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <SkillSlots skill="Mining">
                <Slot name="Head" required="true"/>
              </SkillSlots>
            </Types>
            """));
    }

    [Test]
    public void Parse_SkillRegistersIntoSkillIdEnum()
    {
        Parser parser = Parse("""
            <Types>
              <Skill name="Mining"/>
              <Skill name="LumberJacking"/>
            </Types>
            """);

        Assert.That(parser.Model.Skills, Has.Count.EqualTo(2));
        Assert.That(parser.Model.Enums["SkillId"].GetEnum("Mining"), Is.Not.Null);
        Assert.That(parser.Model.Enums["SkillId"].GetEnum("LumberJacking"), Is.Not.Null);
    }

    [Test]
    public void Parse_ExplicitEnumIsStillSupported()
    {
        Parser parser = Parse("""
            <Types>
              <Enum name="Weather">
                <Value name="Sunny"/>
                <Value name="Rainy"/>
              </Enum>
            </Types>
            """);

        Assert.That(parser.Model.Enums["Weather"].GetEnum("Sunny"), Is.Not.Null);
        Assert.That(parser.Model.Enums["Weather"].GetEnum("Rainy"), Is.Not.Null);
    }
}
