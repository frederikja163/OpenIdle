using System.IO;
using System.Text;
using System.Xml;
using Generator.Core;
using Parser = Generator.Core.Parser;

namespace OpenIdle.Tests.Generators;

/// <summary>
/// The game board reads its activity definitions from this emitter's ACTIVITY_DATA, so what is
/// asserted here is the part of that data the board cannot get from the wire: the duration it
/// draws the progress meter over and the item costs it locks a card behind.
/// </summary>
[TestFixture]
public sealed class TsEmitterTests
{
    private const string Contract = """
        <Types>
          <Enum name="SkillId">
            <Value name="Mining"/>
          </Enum>
          <Enum name="ItemId">
            <Value name="Rock"/>
            <Value name="Stone"/>
          </Enum>
          <Activity name="Stone" time="2.5">
            <ItemCost item="Rock" cost="3"/>
            <ItemReward item="Stone" count="2"/>
            <XpReward skill="Mining" count="10"/>
          </Activity>
          <Activity name="Rock" time="8">
            <ItemReward item="Rock" count="1"/>
            <XpReward skill="Mining" count="5"/>
          </Activity>
        </Types>
        """;

    [Test]
    public void Emit_ActivityDefinition_DeclaresTimeAndCosts()
    {
        string output = Emit();

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("export interface ItemCost {"));
            Assert.That(output, Does.Contain("\tcosts: ItemCost[];"));
            Assert.That(output, Does.Contain("\ttime: number;"));
        });
    }

    [Test]
    public void Emit_Activity_CarriesItsTimeAndCosts()
    {
        string output = Emit();

        Assert.Multiple(() =>
        {
            // Invariant formatting: a comma decimal separator would not be TypeScript.
            Assert.That(output, Does.Contain("time: 2.5,"));
            Assert.That(output, Does.Contain("costs: [{ item: 'Rock', count: 3 }]"));
        });
    }

    [Test]
    public void Emit_ActivityWithoutCosts_EmitsAnEmptyArray()
    {
        string output = Emit();

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("time: 8,"));
            Assert.That(output, Does.Contain("costs: []"));
        });
    }

    private static string Emit()
    {
        XmlDocument document = new();
        document.LoadXml(Contract);

        Parser parser = new();
        parser.Parse(document.DocumentElement!);

        StringWriter writer = new(new StringBuilder());
        using (TsEmitter emitter = new(writer))
        {
            emitter.EmitDtos(parser.Model);
        }

        return writer.ToString();
    }
}
