using System.Text;
using System.Xml;
using CommandLine;
using Generator.Core;
using DtoParser = Generator.Core.Parser;

public enum Target
{
    Cs,
    Ts,
    TsSchema,
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

        [Option('t', "target", Required = true, HelpText = "Which emitter to run: Cs, Ts, or TsSchema.")]
        public Target Target { get; set; }
    }

    public static int Main(string[] args)
    {
        ParserResult<Options> result = CommandLineParser.ParseArguments<Options>(args);
        return result.MapResult(Run, _ => 1);
    }

    private static int Run(Options options)
    {
        DtoModel model;
        try
        {
            model = ParseContract(options.Input);
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"DTO contract file not found: {options.Input}");
            return 1;
        }
        catch (ParserException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (XmlException ex)
        {
            Console.Error.WriteLine($"Invalid XML: {ex.Message}");
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
            Emit(options.Target, output, model);
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

    private static DtoModel ParseContract(string path)
    {
        DtoParser parser = new();
        using FileStream stream = File.OpenRead(path);
        parser.Parse(stream);
        return parser.Model;
    }

    private static TextWriter CreateOutput(string? path)
    {
        return path is null
            ? Console.Out
            : new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void Emit(Target target, TextWriter writer, DtoModel model)
    {
        using IDtoEmitter emitter = CreateEmitter(target, writer);
        emitter.EmitDtos(model);
    }

    private static IDtoEmitter CreateEmitter(Target target, TextWriter writer)
    {
        return target switch
        {
            Target.Cs => new CsEmitter(writer),
            Target.Ts => new TsEmitter(writer),
            Target.TsSchema => new TsSchemaEmitter(writer),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }
}
