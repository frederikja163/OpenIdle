namespace Backend.Dtos;

public sealed class ErrorResponse(string message) : ResponseBase
{
    public string Message { get; } = message;
}
