using System.Collections.Generic;
using System.Linq;

namespace Generator.Core;

internal enum CasingStyle
{
    UpperCamelCase,
    LowerCamelCase,
}

public sealed class Casing
{
    private readonly IReadOnlyList<string> _normalized; 
    
    public Casing(string str)
    {
        _normalized = Normalize(str).ToList();
    }

    public string UpperCamelCase => string.Join("", _normalized.Select(CapitalizeFirst));
    public string LowerCamelCase => string.Join("", _normalized.Select((s, i) => i == 0 ? s : CapitalizeFirst(s)));

    private static string CapitalizeFirst(string str)
    {
        if (str.Length == 1)
        {
            return str.ToUpperInvariant();
        }
        return char.ToUpperInvariant(str[0]) + str.Substring(1);
    }

    private static IEnumerable<string> Normalize(string value)
    {
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]))
            {
                int length = i - start;
                if (length > 0)
                {
                    yield return value.Substring(start, length).ToLowerInvariant();
                }
                start = i;
            }
        }

        yield return value.Substring(start).ToLowerInvariant();
    }
}