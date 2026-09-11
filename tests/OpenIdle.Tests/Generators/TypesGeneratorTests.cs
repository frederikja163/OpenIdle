using Backend.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace OpenIdle.Tests.Generators;

[TestFixture]
public sealed class TypesGeneratorTests
{
    [Test]
    public void Generate_InvalidItemCost_ReportsDtc002()
    {
        AssertInvalidContract("""
            <Types>
              <Activity name="Stone" time="2.5">
                <ItemCost item="Food" cost="invalid"/>
              </Activity>
            </Types>
            """);
    }

    [Test]
    public void Generate_InvalidRewardWeight_ReportsDtc002()
    {
        AssertInvalidContract("""
            <Types>
              <DropTable name="StoneTable">
                <ItemReward item="Stone" weight="invalid" count="1"/>
              </DropTable>
            </Types>
            """);
    }

    private static void AssertInvalidContract(string xml)
    {
        CSharpCompilation compilation = CSharpCompilation.Create("GeneratorTests");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new TypesGenerator().AsSourceGenerator()],
            additionalTexts: [new ContractAdditionalText(xml)]);

        GeneratorDriverRunResult result = driver.RunGenerators(compilation).GetRunResult();

        Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(["DTC002"]));
        Assert.That(result.Results.Single().Exception, Is.Null);
    }

    private sealed class ContractAdditionalText(string content) : AdditionalText
    {
        public override string Path => "types.xml";

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) =>
            SourceText.From(content);
    }
}
