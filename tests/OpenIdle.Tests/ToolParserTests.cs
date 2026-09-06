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
        // The parser reads the document out of the stream during Parse (XmlDocument.Load), so it is
        // safe to dispose the stream here; the parsed element tree is retained by the parser.
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

    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void Parse_NonFiniteStatValue_Throws(string value)
    {
        Assert.Throws<ParserException>(() => Parse($$"""
            <Types>
              <Item name="Rock">
                <Stat name="speed" value="{{value}}"/>
              </Item>
            </Types>
            """));
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
    public void Parse_SkillWithSlots_CollectsSlotBindings()
    {
        Parser parser = Parse("""
            <Types>
              <Skill name="Mining">
                <Slot name="Head" required="true">
                  <Tag name="head"/>
                </Slot>
                <Slot name="Handle">
                  <Tag name="handle"/>
                </Slot>
              </Skill>
            </Types>
            """);

        Skill skill = parser.Model.Skills["Mining"];
        Assert.That(skill.Slots, Has.Count.EqualTo(2));
        Assert.That(skill.Slots[0].Name, Is.EqualTo("Head"));
        Assert.That(skill.Slots[0].Tag.Name, Is.EqualTo("head"));
        Assert.That(skill.Slots[0].Required, Is.True);
        Assert.That(skill.Slots[1].Tag.Name, Is.EqualTo("handle"));
        Assert.That(skill.Slots[1].Required, Is.False);
    }

    [Test]
    public void Parse_ItemSlotRegistersIntoItemSlotIdEnum()
    {
        Parser parser = Parse("""
            <Types>
              <Skill name="Mining">
                <Slot name="Head" required="true">
                  <Tag name="head"/>
                </Slot>
              </Skill>
            </Types>
            """);

        Assert.That(parser.Model.Enums["ItemSlotId"].GetEnum("Head"), Is.Not.Null);
    }

    [Test]
    public void Parse_SlotWithoutTag_Throws()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <Skill name="Mining">
                <Slot name="Head" required="true"/>
              </Skill>
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
    public void Parse_Activity_CollectsItemCosts()
    {
        Parser parser = Parse("""
            <Types>
              <Activity name="Stone" time="2.5">
                <ItemCost item="Food" cost="1"/>
                <ItemCost item="Wood" cost="3"/>
              </Activity>
            </Types>
            """);

        Activity activity = parser.Model.Activities["Stone"];
        Assert.That(activity.Costs, Has.Count.EqualTo(2));
        Assert.That(activity.Costs[0].Item, Is.EqualTo("Food"));
        Assert.That(activity.Costs[0].Count, Is.EqualTo(1));
        Assert.That(activity.Costs[1].Item, Is.EqualTo("Wood"));
        Assert.That(activity.Costs[1].Count, Is.EqualTo(3));
    }

    [Test]
    public void Parse_Activity_DuplicateItemCostsAreAggregated()
    {
        Parser parser = Parse("""
            <Types>
              <Activity name="Stone" time="2.5">
                <ItemCost item="Food" cost="1"/>
                <ItemCost item="Food" cost="2"/>
                <ItemCost item="Wood" cost="3"/>
              </Activity>
            </Types>
            """);

        Activity activity = parser.Model.Activities["Stone"];
        Assert.That(activity.Costs, Has.Count.EqualTo(2));
        Assert.That(activity.Costs[0].Item, Is.EqualTo("Food"));
        Assert.That(activity.Costs[0].Count, Is.EqualTo(3));
        Assert.That(activity.Costs[1].Item, Is.EqualTo("Wood"));
        Assert.That(activity.Costs[1].Count, Is.EqualTo(3));
    }

    [Test]
    public void Parse_Activity_NegativeCost_ThrowsParserException()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <Activity name="Stone" time="2.5">
                <ItemCost item="Food" cost="-1"/>
              </Activity>
            </Types>
            """));
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
