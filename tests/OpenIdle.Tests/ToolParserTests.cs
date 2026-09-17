using System.IO;
using System.Text;
using System.Xml.Serialization;
using Generator.Core;
using Generator.Core.Spec;

namespace OpenIdle.Tests;

[TestFixture]
public sealed class ToolParserTests
{
    private static Root Parse(string xml)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        using MemoryStream stream = new(bytes);
        XmlSerializer serializer = new(typeof(Root));
        Root root = (Root)serializer.Deserialize(stream)!;

        new VisitorPipeline(new AddEnumsVisitor(), new ValidationVisitor()).Visit(root);
        return root;
    }

    [Test]
    public void Parse_PopulatesItemStatsAndOrderedTags()
    {
        Root root = Parse("""
            <Types>
              <Item name="IronPickaxeHead">
                <Tag name="Head"/>
                <Tag name="Iron"/>
                <Stat name="Speed" value="1.1"/>
                <Stat name="Durable" value="1.5"/>
              </Item>
            </Types>
            """);

        Item item = root.Items.Single(i => i.Name == "IronPickaxeHead");
        Assert.That(item.Name, Is.EqualTo("IronPickaxeHead"));
        Assert.That(item.Tags, Has.Count.EqualTo(2));
        Assert.That(item.Tags[0].Name, Is.EqualTo("Head"));
        Assert.That(item.Tags[1].Name, Is.EqualTo("Iron"));
        Assert.That(item.Stats, Has.Count.EqualTo(2));
        Assert.That(item.Stats[0].Name, Is.EqualTo("Speed"));
        Assert.That(item.Stats[0].Value, Is.EqualTo(1.1f));
        Assert.That(item.Stats[1].Name, Is.EqualTo("Durable"));
        Assert.That(item.Stats[1].Value, Is.EqualTo(1.5f));
    }

    [Test]
    public void Parse_TagsRegisterIntoItemTagIdEnumDeduplicated()
    {
        Root root = Parse("""
            <Types>
              <Item name="Stone">
                <Tag name="Ore"/>
              </Item>
              <Item name="BrokenRock">
                <Tag name="Ore"/>
              </Item>
              <Item name="IronPickaxeHead">
                <Tag name="Head"/>
                <Tag name="Iron"/>
              </Item>
            </Types>
            """);

        Generator.Core.Spec.Enum tagEnum = root.Enums.Single(e => e.Name == "ItemTagId");
        Assert.That(tagEnum.Values.Select(v => v.Name), Is.EqualTo(new[] { "None", "Ore", "Head", "Iron" }));
        Assert.That(root.Items.Single(i => i.Name == "Stone").Tags[0].Name, Is.EqualTo("Ore"));
        Assert.That(root.Items.Single(i => i.Name == "BrokenRock").Tags[0].Name, Is.EqualTo("Ore"));
    }

    [Test]
    public void Parse_UnknownStat_Throws()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <Item name="Rock">
                <Stat name="Bogus" value="1.0"/>
              </Item>
            </Types>
            """));
    }

    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void Parse_NonFiniteStatValue_Throws(string value)
    {
        Assert.Throws<ParserException>(() => Parse($$"""
            <Types>
              <Item name="Rock">
                <Stat name="Speed" value="{{value}}"/>
              </Item>
            </Types>
            """));
    }

    [Test]
    public void Parse_ItemRegistersIntoItemIdEnum()
    {
        Root root = Parse("""
            <Types>
              <Item name="OakHandle"/>
            </Types>
            """);

        Assert.That(root.Enums.Single(e => e.Name == "ItemId").Values.Select(v => v.Name), Does.Contain("OakHandle"));
    }

    [Test]
    public void Parse_SkillWithSlots_CollectsSlotBindings()
    {
        Root root = Parse("""
            <Types>
              <Skill name="Mining">
                <Slot name="Head" required="true">
                  <Tag name="Head"/>
                </Slot>
                <Slot name="Handle">
                  <Tag name="Handle"/>
                </Slot>
              </Skill>
            </Types>
            """);

        Skill skill = root.Skills.Single(s => s.Name == "Mining");
        Assert.That(skill.Slots, Has.Count.EqualTo(2));
        Assert.That(skill.Slots[0].Name, Is.EqualTo("Head"));
        Assert.That(skill.Slots[0].AcceptedTag.Name, Is.EqualTo("Head"));
        Assert.That(skill.Slots[0].Required, Is.True);
        Assert.That(skill.Slots[1].Name, Is.EqualTo("Handle"));
        Assert.That(skill.Slots[1].AcceptedTag.Name, Is.EqualTo("Handle"));
        Assert.That(skill.Slots[1].Required, Is.False);
    }

    [Test]
    public void Parse_ItemSlotRegistersIntoItemSlotIdEnum()
    {
        Root root = Parse("""
            <Types>
              <Skill name="Mining">
                <Slot name="Head" required="true">
                  <Tag name="Head"/>
                </Slot>
              </Skill>
            </Types>
            """);

        Assert.That(root.Enums.Single(e => e.Name == "ItemSlotId").Values.Select(v => v.Name), Does.Contain("Head"));
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
        Root root = Parse("""
            <Types>
              <Skill name="Mining"/>
              <Skill name="LumberJacking"/>
            </Types>
            """);

        Assert.That(root.Skills, Has.Count.EqualTo(2));
        Assert.That(root.Enums.Single(e => e.Name == "SkillId").Values.Select(v => v.Name), Does.Contain("Mining"));
        Assert.That(root.Enums.Single(e => e.Name == "SkillId").Values.Select(v => v.Name), Does.Contain("LumberJacking"));
    }

    [Test]
    public void Parse_Activity_CollectsItemCosts()
    {
        Root root = Parse("""
            <Types>
              <Activity name="Stone" time="2.5">
                <ItemCost item="Food" cost="1"/>
                <ItemCost item="Wood" cost="3"/>
              </Activity>
            </Types>
            """);

        Activity activity = root.Activities.Single(a => a.Name == "Stone");
        Assert.That(activity.ItemCosts, Has.Count.EqualTo(2));
        Assert.That(activity.ItemCosts[0].Item, Is.EqualTo("Food"));
        Assert.That(activity.ItemCosts[0].Cost, Is.EqualTo(1));
        Assert.That(activity.ItemCosts[1].Item, Is.EqualTo("Wood"));
        Assert.That(activity.ItemCosts[1].Cost, Is.EqualTo(3));
    }

    [Test]
    public void Parse_Activity_DuplicateItemCosts_Throws()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <Activity name="Stone" time="2.5">
                <ItemCost item="Food" cost="1"/>
                <ItemCost item="Food" cost="2"/>
              </Activity>
            </Types>
            """));
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
        Root root = Parse("""
            <Types>
              <Enum name="Weather">
                <Value name="Sunny"/>
                <Value name="Rainy"/>
              </Enum>
            </Types>
            """);

        Generator.Core.Spec.Enum weather = root.Enums.Single(e => e.Name == "Weather");
        Assert.That(weather.Values.Select(v => v.Name), Does.Contain("Sunny"));
        Assert.That(weather.Values.Select(v => v.Name), Does.Contain("Rainy"));
    }
}
