using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using Generator.Core;
using Generator.Core.Spec;

namespace OpenIdle.Tests.Generators;

/// <summary>
/// The hosted schema is JsonSchemaEmitter's serialization of the parsed contract. It is what
/// the debug console fetches from the backend, so these assert the parts of the contract the
/// model itself carries: the implicit None member, the enums synthesised from
/// &lt;DropTable&gt;/&lt;Activity&gt;/&lt;Item&gt;/&lt;Skill&gt;, and the named request response.
/// </summary>
[TestFixture]
public sealed class JsonSchemaEmitterTests
{
    private const string Contract = """
        <Types>
          <Skill name="Mining"/>
          <Item name="Rock"/>
          <Item name="Stone">
            <Tag name="Ore"/>
          </Item>
          <Item name="BrokenRock">
            <Tag name="Ore"/>
          </Item>
          <DropTable name="StoneTable">
            <ItemReward item="Stone" weight="5" count="2"/>
          </DropTable>
          <Activity name="Stone" time="2.5">
            <XpReward skill="Mining" count="10"/>
          </Activity>
          <Dto name="ProfileDto">
            <Property name="Name" type="String"/>
            <Property name="ProfileId" type="Guid"/>
          </Dto>
          <Request name="GetSkillsRequest">
            <Property name="SkillIds" type="SkillId" multiple="true" optional="true"/>
            <Response name="GetSkillsResponse">
              <Property name="Profiles" type="ProfileDto" multiple="true"/>
            </Response>
          </Request>
          <Event name="ProfilesChangedEvent">
            <Property name="Profiles" type="ProfileDto" multiple="true"/>
          </Event>
        </Types>
        """;

    [Test]
    public void Emit_Enums_LeadWithTheImplicitNoneMember()
    {
        Assert.That(EnumValues("SkillId"), Is.EqualTo(new[] { "None", "Mining" }));
    }

    [Test]
    public void Emit_DropTableAndActivity_SynthesiseTheirOwnEnums()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EnumValues("DropTableId"), Is.EqualTo(new[] { "None", "StoneTable" }));
            Assert.That(EnumValues("ActivityId"), Is.EqualTo(new[] { "None", "Stone" }));
        });
    }

    [Test]
    public void Emit_TagEnum_DeduplicatesSharedTags()
    {
        Assert.That(EnumValues("ItemTagId"), Is.EqualTo(new[] { "None", "Ore" }));
    }

    [Test]
    public void Emit_Request_CarriesItsNamedResponse()
    {
        JsonElement request = Requests().Single(r => r.GetProperty("Name").GetString() == "GetSkillsRequest");
        JsonElement response = request.GetProperty("Responses").EnumerateArray().Single();

        Assert.Multiple(() =>
        {
            Assert.That(response.GetProperty("Name").GetString(), Is.EqualTo("GetSkillsResponse"));
            JsonElement property = response.GetProperty("Properties").EnumerateArray().Single();
            Assert.That(property.GetProperty("Name").GetString(), Is.EqualTo("Profiles"));
            Assert.That(property.GetProperty("Type").GetString(), Is.EqualTo("ProfileDto"));
        });
    }

    [Test]
    public void Emit_ComponentNames_KeepTheirExplicitSuffix()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Dtos().Select(d => d.GetProperty("Name").GetString()), Does.Contain("ProfileDto"));
            Assert.That(Requests().Select(r => r.GetProperty("Name").GetString()), Does.Contain("GetSkillsRequest"));
            Assert.That(Events().Select(e => e.GetProperty("Name").GetString()), Does.Contain("ProfilesChangedEvent"));
        });
    }

    private static string[] EnumValues(string typeName)
    {
        JsonElement en = Root().GetProperty("Enums").EnumerateArray()
            .Single(e => e.GetProperty("Name").GetString() == typeName);
        return en.GetProperty("Values").EnumerateArray()
            .Select(v => v.GetProperty("Name").GetString()!)
            .ToArray();
    }

    private static IEnumerable<JsonElement> Dtos() => Root().GetProperty("Dtos").EnumerateArray();

    private static IEnumerable<JsonElement> Requests() => Root().GetProperty("Requests").EnumerateArray();

    private static IEnumerable<JsonElement> Events() => Root().GetProperty("Events").EnumerateArray();

    private static JsonElement Root()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(Contract));
        XmlSerializer serializer = new(typeof(Root));
        Root root = (Root)serializer.Deserialize(stream)!;

        new VisitorPipeline(new AddEnumsVisitor(), new ValidationVisitor()).Visit(root);

        using JsonDocument document = JsonDocument.Parse(JsonSchemaEmitter.Emit(root));
        return document.RootElement.Clone();
    }
}
