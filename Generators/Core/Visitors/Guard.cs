namespace Generator.Core;

internal static class Guard
{
    public static void Required(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ParserException($"{description} is required.");
        }
    }

    public static void NotNull(object? value, string description)
    {
        if (value is null)
        {
            throw new ParserException($"{description} is required.");
        }
    }
}
