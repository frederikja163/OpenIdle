using System;
using System.IO;

namespace Generator.Core;

/// <summary>
/// Where a scope's opening bracket goes. C# is written Allman, TypeScript K&R —
/// prettier reformats anything else, so the TS emitter cannot use the default.
/// </summary>
internal enum BraceStyle
{
    NextLine,
    SameLine
}

internal sealed class ScopedTextWriter
{
    private readonly TextWriter _writer;
    private bool _newLine = true;
    private string? _indentUnit;

    public ScopedTextWriter(TextWriter writer)
    {
        _writer = writer;
    }

    public int SpacesPerTab { get; set; } = 4;

    /// <summary>
    /// One level of indentation. Defaults to <see cref="SpacesPerTab"/> spaces;
    /// set it to "\t" for output that has to satisfy a tab-indenting formatter.
    /// </summary>
    public string IndentUnit
    {
        get => _indentUnit ?? new string(' ', SpacesPerTab);
        set => _indentUnit = value;
    }

    public BraceStyle BraceStyle { get; set; } = BraceStyle.NextLine;

    public int Indentations { get; internal set; }

    private void TryWriteIndents()
    {
        if (_newLine)
        {
            for (int i = 0; i < Indentations; i++)
            {
                _writer.Write(IndentUnit);
            }
            _newLine = false;
        }
    }
    
    public void Write(object value)
    {
        TryWriteIndents();
        _writer.Write(value);
    }

    public void WriteLine()
    {
        TryWriteIndents();
        _writer.WriteLine();
        _newLine = true;
    }

    public void WriteLine(object value)
    {
        TryWriteIndents();
        _writer.WriteLine(value);
        _newLine = true;
    }

    /// <param name="endText">Appended to the closing bracket, for languages that
    /// terminate a block with more than the bracket itself ("};", "],").</param>
    public Scope Scope(string startText = "", ScopeStyle scopeStyle = ScopeStyle.Curly, string endText = "")
    {
        // Same-line placement has to happen here rather than in Scope's constructor:
        // the bracket goes on the header's line, so it must be written before that
        // line is terminated, and Scope only ever runs after it already has been.
        if (BraceStyle == BraceStyle.SameLine && Opener(scopeStyle) is char open)
        {
            WriteLine(startText.Length == 0 ? open.ToString() : startText + " " + open);
            return new Scope(scopeStyle, this, endText, writeOpener: false);
        }

        WriteLine(startText);
        return new Scope(scopeStyle, this, endText);
    }

    internal static char? Opener(ScopeStyle style)
    {
        return style switch
        {
            ScopeStyle.Curly => '{',
            ScopeStyle.Parenthesis => '(',
            ScopeStyle.Square => '[',
            ScopeStyle.Angle => '<',
            ScopeStyle.Indentation => null,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
        };
    }

    internal static char? Closer(ScopeStyle style)
    {
        return style switch
        {
            ScopeStyle.Curly => '}',
            ScopeStyle.Parenthesis => ')',
            ScopeStyle.Square => ']',
            ScopeStyle.Angle => '>',
            ScopeStyle.Indentation => null,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
        };
    }
}

internal enum ScopeStyle
{
    Curly,
    Parenthesis,
    Square,
    Angle,
    Indentation
}

internal sealed class Scope : IDisposable
{
    private readonly ScopeStyle _style;
    private readonly ScopedTextWriter _writer;
    private readonly string _endText;

    public Scope(ScopeStyle style, ScopedTextWriter writer)
        : this(style, writer, endText: "")
    {
    }

    internal Scope(ScopeStyle style, ScopedTextWriter writer, string endText, bool writeOpener = true)
    {
        _style = style;
        _writer = writer;
        _endText = endText;
        if (writeOpener && ScopedTextWriter.Opener(style) is char open)
        {
            _writer.WriteLine(open);
        }
        _writer.Indentations++;
    }

    public void Dispose()
    {
        _writer.Indentations--;
        if (ScopedTextWriter.Closer(_style) is char close)
        {
            _writer.WriteLine(close + _endText);
        }
    }
}