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
    
    public int Indentations { get; set; }

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
    }

    public void WriteLine(object value)
    {
        TryWriteIndents();
        _writer.WriteLine(value);
        _newLine = true;
    }

    public Scope Scope(string startText = "", ScopeStyle scopeStyle = ScopeStyle.Curley)
    {
        WriteLine(startText);
        return new Scope(scopeStyle, this);
    }
}

internal enum ScopeStyle
{
    Curley,
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
            case ScopeStyle.Curley:
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
        _writer.Indentations += 1;
    }

    public void Dispose()
    {
        _writer.Indentations -= 1;
        switch (_style)
        {
            case ScopeStyle.Curley:
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