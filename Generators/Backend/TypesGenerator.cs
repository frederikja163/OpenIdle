using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Generator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Backend.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class TypesGenerator : IIncrementalGenerator
{
    private const string TypesFileName = "types.xml";
    private const string DtoOutputName = "Dto.g.cs";

    private static readonly DiagnosticDescriptor DtoFileMissing = new(
        id: "DTC001",
        title: "DTO file is missing",
        messageFormat: "No file named '{0}' was found among the project's AdditionalFiles",
        category: "Dto",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ParseError = new(
        id: "DTC002",
        title: "DTO file is invalid",
        messageFormat: "{0}",
        category: "Dto",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableArray<string>> contractXml = context.AdditionalTextsProvider
            .Where(file =>
                string.Equals(Path.GetFileName(file.Path), TypesFileName, StringComparison.OrdinalIgnoreCase))
            .Select((file, _) => file.GetText()?.ToString() ?? string.Empty)
            .Collect()
            .WithTrackingName("DtoContractXml");

        context.RegisterSourceOutput(contractXml, static (productionContext, xml) =>
        {
            if (xml.IsEmpty)
            {
                productionContext.ReportDiagnostic(
                    Diagnostic.Create(DtoFileMissing, Location.None, TypesFileName));
                return;
            }

            Parser parser = new Parser();
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(xml[0]);
                using MemoryStream stream = new MemoryStream(bytes);
                parser.Parse(stream);
            }
            catch (ParserException ex)
            {
                productionContext.ReportDiagnostic(
                    Diagnostic.Create(ParseError, Location.None, ex.Message));
                return;
            }

            using StringWriter writer = new StringWriter();
            CsEmitter emitter = new CsEmitter(writer);
            emitter.EmitDtos(parser.Model);
            emitter.Dispose();
            
            productionContext.AddSource(DtoOutputName, SourceText.From(writer.ToString(), Encoding.UTF8));
        });
    }
}
