using System.IO;
using System.Text;
using System.Xml;
using Generator.Core;
using Parser = Generator.Core.Parser;

namespace OpenIdle.Tests.Generators;

/// <summary>
/// The debug console builds its request forms from this emitter's output, so what is asserted
/// here is the part of the contract the console reads: the property kinds it switches on, the
/// wire names it puts in a frame, and the enum members it offers.
///
/// Several of these are rules types.xml never states — the implicit None member, the enums
/// synthesised from &lt;DropTable&gt;/&lt;Activity&gt;. They came from the model for free once
/// the console stopped re-deriving them in TypeScript, and these tests are what keeps them
/// arriving.
/// </summary>
[TestFixture]
public sealed class TsSchemaEmitterTests
{
    private const string Contract = """
        <Types>
          <Enum name="SkillId">
            <Value name="Mining"/>
          </Enum>
          <DropTable name="StoneTable">
            <ItemReward item="Stone" weight="5" count="2"/>
          </DropTable>
          <Activity name="Stone" time="2.5">
            <XpReward skill="Mining" count="10"/>
          </Activity>
          <Dto name="Profile">
            <Property name="Name" type="String"/>
            <Property name="ProfileId" type="Guid"/>
            <Property name="StartedAt" type="Timestamp"/>
          </Dto>
          <Request name="GetSkills">
            <Property name="SkillIds" type="SkillId" multiple="true" optional="true"/>
            <Response>
              <Property name="Profiles" type="Profile" multiple="true"/>
            </Response>
          </Request>
          <Request name="Ping">
            <Response name="Pong"/>
          </Request>
          <Event name="ProfilesChanged">
            <Property name="Profiles" type="Profile" multiple="true"/>
          </Event>
        </Types>
        """;

    [Test]
    public void Emit_Enum_LeadsWithTheImplicitNoneMember()
    {
        Assert.That(Emit(), Does.Contain("SkillId: { typeName: 'SkillId', values: ['None', 'Mining'] }"));
    }

    [Test]
    public void Emit_DropTableAndActivity_SynthesiseTheirOwnEnums()
    {
        string output = Emit();

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("DropTableId: { typeName: 'DropTableId', values: ['None', 'StoneTable'] }"));
            Assert.That(output, Does.Contain("ActivityId: { typeName: 'ActivityId', values: ['None', 'Stone'] }"));
        });
    }

    [Test]
    public void Emit_Property_CarriesTheWireNameMultipleAndOptional()
    {
        Assert.That(Emit(), Does.Contain(
            "{ name: 'SkillIds', wireName: 'skillIds', kind: 'enum', typeName: 'SkillId', multiple: true, optional: true }"));
    }

    [Test]
    public void Emit_CustomProperty_ResolvesToTheGeneratedDtoName()
    {
        Assert.That(Emit(), Does.Contain(
            "{ name: 'Profiles', wireName: 'profiles', kind: 'dto', typeName: 'ProfileDto', multiple: true, optional: false }"));
    }

    [Test]
    public void Emit_BuiltInProperty_MapsToItsOwnKind()
    {
        string output = Emit();

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("wireName: 'name', kind: 'string'"));
            // Not folded into 'guid': the console offers a profile picker for one and a plain
            // text box for the other, so the distinction has to survive the emit.
            Assert.That(output, Does.Contain("wireName: 'profileId', kind: 'guid'"));
            // A Timestamp is a plain number to the console; the XML token survives as the label.
            Assert.That(output, Does.Contain("wireName: 'startedAt', kind: 'long', typeName: 'Timestamp'"));
        });
    }

    [Test]
    public void Emit_Names_CarryTheirGeneratedSuffix()
    {
        string output = Emit();

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("typeName: 'GetSkillsRequest'"));
            Assert.That(output, Does.Contain("typeName: 'GetSkillsResponse'"));
            Assert.That(output, Does.Contain("typeName: 'ProfilesChangedEvent'"));
            Assert.That(output, Does.Contain("ProfileDto: {"));
        });
    }

    [Test]
    public void Emit_NamedResponse_OverridesTheRequestsName()
    {
        Assert.That(Emit(), Does.Contain("typeName: 'PongResponse'"));
    }

    [Test]
    public void Emit_Output_AnnotatesItselfAgainstTheHandWrittenInterface()
    {
        // The annotation is what makes a drift between this emitter and the frontend's
        // ProtocolSchema a compile error over there rather than a broken page.
        string output = Emit();

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("import type { ProtocolSchema } from './schema';"));
            Assert.That(output, Does.Contain("export const PROTOCOL: ProtocolSchema = {"));
        });
    }

    private static string Emit()
    {
        XmlDocument document = new();
        document.LoadXml(Contract);

        Parser parser = new();
        parser.Parse(document.DocumentElement!);

        StringWriter writer = new(new StringBuilder());
        using (TsSchemaEmitter emitter = new(writer))
        {
            emitter.EmitDtos(parser.Model);
        }

        return writer.ToString();
    }
}
