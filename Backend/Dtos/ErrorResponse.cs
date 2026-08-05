namespace Backend.Dtos;

public sealed class ErrorResponse(string? code, string message) : ResponseBase
{
    public string? Code { get; } = code;
    public string Message { get; } = message;
}
