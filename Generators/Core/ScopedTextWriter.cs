using System;
using System.IO;

namespace Generator.Core;

internal sealed class ScopedTextWriter
{
    private readonly TextWriter _writer;
    private bool _newLine = true;

    public ScopedTextWriter(TextWriter writer)
    {
        _writer = writer;
    }

    public int SpacesPerTab { get; set; } = 4;

    public int Indentations { get; internal set; }

    private void TryWriteIndents()
    {
        if (_newLine)
        {
            _writer.Write(new string(' ', Indentations * SpacesPerTab));
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

    public Scope Scope(string startText = "", ScopeStyle scopeStyle = ScopeStyle.Curly)
    {
        WriteLine(startText);
        return new Scope(scopeStyle, this);
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

    public Scope(ScopeStyle style, ScopedTextWriter writer)
    {
        _style = style;
        _writer = writer;
        switch (style)
        {
            case ScopeStyle.Curly:
                _writer.WriteLine('{');
                break;
            case ScopeStyle.Parenthesis:
                _writer.WriteLine('(');
                break;
            case ScopeStyle.Square:
                _writer.WriteLine('[');
                break;
            case ScopeStyle.Angle:
                _writer.WriteLine('<');
                break;
            case ScopeStyle.Indentation:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(style), style, null);
        }
        _writer.Indentations++;
    }

    public void Dispose()
    {
        _writer.Indentations--;
        switch (_style)
        {
            case ScopeStyle.Curly:
                _writer.WriteLine('}');
                break;
            case ScopeStyle.Parenthesis:
                _writer.WriteLine(')');
                break;
            case ScopeStyle.Square:
                _writer.WriteLine(']');
                break;
            case ScopeStyle.Angle:
                _writer.WriteLine('>');
                break;
            case ScopeStyle.Indentation:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}