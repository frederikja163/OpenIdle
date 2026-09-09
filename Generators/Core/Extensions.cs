using System.Collections.Generic;

namespace Generator.Core;

public static class Extensions
{
    public static void AcceptAll<T>(this List<T> nodes, IVisitor visitor) where T : INode
    {
        foreach (var node in nodes)
            node.Accept(visitor);
    }

    public static string ToCamelCase(this string name)
    {
        if (char.IsUpper(name[0]))
        {
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        return name;
    }
}