using System;
using System.Collections.Generic;
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
        return (T)Convert.ChangeType(RequireAttribute(element, name), typeof(T));
    }

    public static T? GetAttribute<T>(this XmlElement element, string name, T? @default = default)
        where T : IConvertible
    {
        string attribute = element.GetAttribute(name);
        return string.IsNullOrEmpty(attribute) ? @default : (T)Convert.ChangeType(attribute, typeof(T));
    }
}