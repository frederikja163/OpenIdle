namespace Backend;

public sealed class AuthOptions
{
    public bool AllowTestLogin { get; init; }
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
}