using System.IO;
using System.Text;
using System.Xml.Serialization;
using Generator.Core;

namespace OpenIdle.Tests;

/// <summary>
/// Rules doc/backend/dto-contract.md claims the contract enforces, which no other test covers.
/// The XmlSerializer-based pipeline does not validate them yet, so these are expected to fail
/// until validation is ported forward from the removed Parser.
/// </summary>
[TestFixture]
public sealed class ContractValidationTests
{
    private static TypesXmlRoot Parse(string xml)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));
        XmlSerializer serializer = new(typeof(TypesXmlRoot));
        TypesXmlRoot root = (TypesXmlRoot)serializer.Deserialize(stream)!;

        AddEnumsVisitor enumsVisitor = new();
        root.Accept(enumsVisitor);
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
