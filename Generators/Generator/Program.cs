using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using CommandLine;
using Generator.Core;

public enum Target
{
    Cs,
    Ts,
    TsSchema,
    Json,
}

public static class Program
{
    private static readonly CommandLine.Parser CommandLineParser = new(settings =>
    {
        settings.HelpWriter = Console.Error;
        settings.CaseInsensitiveEnumValues = true;
    });

    private sealed class Options
    {
        [Option('i', "input", Required = true, HelpText = "Path to the DTO contract XML.")]
        public string Input { get; set; } = "";

        [Option('o', "output", HelpText = "Path to write the generated output to. Defaults to stdout.")]
        public string? Output { get; set; }

        [Option('t', "target", Required = true, HelpText = "Which emitter to run: Cs, Ts, TsSchema, or Json.")]
        public Target Target { get; set; }
    }

    public static int Main(string[] args)
    {
        ParserResult<Options> result = CommandLineParser.ParseArguments<Options>(args);
        return result.MapResult(Run, _ => 1);
    }

    private static int Run(Options options)
    {
        TypesXmlRoot root;
        try
        {
            root = ParseContract(options.Input);
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"DTO contract file not found: {options.Input}");
            return 1;
        }
        catch (InvalidOperationException ex) when (ex.InnerException is XmlException xmlEx)
        {
            Console.Error.WriteLine($"Invalid XML: {xmlEx.Message}");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        TextWriter? output = null;
        try
        {
            output = CreateOutput(options.Output);
            Emit(options.Target, output, root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            if (options.Output is not null)
            {
                output?.Dispose();
            }
        }

        Console.Error.WriteLine(options.Output is not null
            ? $"Generated {Path.GetFullPath(options.Output)}"
            : $"Generated {Path.GetFullPath(options.Input)} to stdout");
        return 0;
    }

    private static TypesXmlRoot ParseContract(string path)
    {
        XmlSerializer serializer = new(typeof(TypesXmlRoot));
        using FileStream stream = File.OpenRead(path);
        return (TypesXmlRoot)serializer.Deserialize(stream)!;
    }

    private static TextWriter CreateOutput(string? path)
    {
        return path is null
            ? Console.Out
            : new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void Emit(Target target, TextWriter writer, TypesXmlRoot root)
    {
        AddEnumsVisitor enumsVisitor = new();
        root.Accept(enumsVisitor);

        switch (target)
        {
            case Target.Cs:
                using (CsEmitterVisitor csEmitter = new(writer))
                {
                    csEmitter.Emit(root);
                }
                break;
            case Target.Ts:
                using (TsEmitterVisitor tsEmitter = new(writer))
                {
                    tsEmitter.Emit(root);
                }
                break;
            case Target.TsSchema:
                Console.Error.WriteLine("TsSchema emitter has not been ported to the visitor pattern yet.");
                break;
            case Target.Json:
                writer.Write(JsonSchemaEmitter.Emit(root));
                break;
        }
    }
}
