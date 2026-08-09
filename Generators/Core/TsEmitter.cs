using System;
using System.Collections.Generic;
using System.IO;

namespace Generator.Core;

public sealed class TsEmitter : IDtoEmitter
{
    private readonly ScopedTextWriter _textWriter;

    public TsEmitter(TextWriter writer)
    {
        _textWriter = new ScopedTextWriter(writer);

        using (Scope _ = _textWriter.Scope("interface DtoBase"))
        {
            _textWriter.WriteLine("$type: string;");
        }

        using (Scope _ = _textWriter.Scope("interface RequestBase"))
        {
            _textWriter.WriteLine("requestId: number;");
        }

        using (Scope _ = _textWriter.Scope("interface ResponseBase"))
        {
            _textWriter.WriteLine("requestId: number;");
        }

        using (Scope _ = _textWriter.Scope("interface EventBase"))
        {
            _textWriter.WriteLine("eventId: number;");
        }
        _textWriter.WriteLine();
    }

    public void EmitDtos(DtoModel model)
    {
        foreach (Object obj in model.AllObjects)
        {
            using (Scope _ = _textWriter.Scope($"interface {GetName(obj)} extends {BaseType(obj)}"))
            {
                foreach (Property property in obj.Properties)
                {
                    Property(property);
                }
            }
            _textWriter.WriteLine();
        }
    }

    private string BaseType(Object obj)
    {
        return obj switch
        {
            Dto => "DtoBase",
            Event => "EventBase",
            Request => "RequestBase",
            Response => "ResponseBase",
            _ => throw new ArgumentOutOfRangeException(nameof(obj))
        };
    }

    private string GetName(Object obj)
    {
        return obj.Name.UpperCamelCase + obj switch
        {
            Dto => "Dto",
            Event => "Event",
            Request => "Request",
            Response => "Response",
            _ => throw new ArgumentOutOfRangeException(nameof(obj))
        };
    }

    private void Property(Property property)
    {
        _textWriter.WriteLine($"{property.Name.LowerCamelCase}: {GetPropertyType(property)};");
    }

    private string GetPropertyType(Property property)
    {
        string str = property.PropertyType switch
        {
            PropertyType.Custom => property.PropertyTypeString.UpperCamelCase + "Dto",
            PropertyType.String => "string",
            PropertyType.Int => "number",
            PropertyType.Float => "number",
            PropertyType.Guid => "string",
            _ => throw new ArgumentOutOfRangeException()
        };
        return property.Multiple ? str + "[]" : str;
    }

    public void Dispose()
    {
    }
}