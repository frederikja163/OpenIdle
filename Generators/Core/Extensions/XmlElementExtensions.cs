using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace Generator.Core.Extensions;

internal static class XmlElementExtensions
{
    public static IEnumerable<XmlElement> GetChildren(this XmlElement element, string name)
    {
        return element.ChildNodes.OfType<XmlElement>().Where(child => child.Name == name);
    }

    public static string RequireAttribute(this XmlElement element, string name)
    {
        string attribute = element.GetAttribute(name);
        if (string.IsNullOrEmpty(attribute))
        {
            throw new ParserException($"Required attribute {name} not found on element {element.Name}");
        }

        return attribute;
    }
    
    public static T RequireAttribute<T>(this XmlElement element, string name)
        where T : IConvertible
    {
        string value = RequireAttribute(element, name);
        return ConvertValue<T>(element, name, value);
    }

    public static T? GetAttribute<T>(this XmlElement element, string name, T? @default = default)
        where T : IConvertible
    {
        string attribute = element.GetAttribute(name);
        return string.IsNullOrEmpty(attribute) ? @default : ConvertValue<T>(element, name, attribute);
    }

    public static bool HasAttribute(this XmlElement element, string name)
    {
        return !string.IsNullOrEmpty(element.GetAttribute(name));
    }

    private static T ConvertValue<T>(XmlElement element, string name, string value)
        where T : IConvertible
    {
        try
        {
            object converted = typeof(T) switch
            {
                _ when typeof(T) == typeof(int) => int.Parse(value, CultureInfo.InvariantCulture),
                _ when typeof(T) == typeof(float) => float.Parse(value, CultureInfo.InvariantCulture),
                // bool.Parse is already culture-invariant: it accepts only "True"/"False" (case-insensitive, ordinal).
                _ when typeof(T) == typeof(bool) => bool.Parse(value),
                _ => Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture),
            };
            return (T)converted;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw new ParserException($"Attribute {name} on element {element.Name} has invalid value '{value}'.");
        }
    }
}