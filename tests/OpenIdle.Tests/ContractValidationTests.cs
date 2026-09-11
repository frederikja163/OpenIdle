using System.IO;
using System.Text;
using System.Xml.Serialization;
using Generator.Core;
using Generator.Core.Spec;

namespace OpenIdle.Tests;

/// <summary>
/// Rules doc/backend/dto-contract.md claims the contract enforces, which no other test covers.
/// </summary>
[TestFixture]
public sealed class ContractValidationTests
{
    private static Root Parse(string xml)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));
        XmlSerializer serializer = new(typeof(Root));
        Root root = (Root)serializer.Deserialize(stream)!;

        new VisitorPipeline(new AddEnumsVisitor(), new ValidationVisitor()).Visit(root);
        return root;
    }

    [Test]
    public void Parse_RequestWithoutResponse_Throws()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <Request name="PingRequest"/>
            </Types>
            """));
    }

    [Test]
    public void Parse_RequestWithTwoResponses_Throws()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <Request name="PingRequest">
                <Response name="PongResponse"/>
                <Response name="OtherResponse"/>
              </Request>
            </Types>
            """));
    }

    [Test]
    public void Parse_PropertyWithoutType_Throws()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <Dto name="ProfileDto">
                <Property name="Name"/>
              </Dto>
            </Types>
            """));
    }

    [Test]
    public void Parse_PropertyWithUnknownType_Throws()
    {
        Assert.Throws<ParserException>(() => Parse("""
            <Types>
              <Dto name="ProfileDto">
                <Property name="Name" type="Bogus"/>
              </Dto>
            </Types>
            """));
    }
}
