using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Backend.Database.Entities;

namespace Backend;

internal sealed class BackendException : Exception
{
    internal BackendException(string message) : base(message)
    {
    }

    public static void ThrowIfNullOrWhiteSpace([NotNull] string? name, [CallerArgumentExpression(nameof(name))] string parameterName = "")
    {
        if (name is null)
        {
            throw new BackendException($"Unexpected null value at {parameterName}.");
        }
    }
}