using System;

namespace Backend.Errors;

/// <summary>
/// A known business failure carrying a stable, machine-readable code. Socket
/// serialises it onto ErrorResponse so clients can branch on the code rather
/// than on message text, which is free to change.
/// </summary>
public sealed class ErrorCodeException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
