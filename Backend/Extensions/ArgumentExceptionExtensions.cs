using System;
using System.Runtime.CompilerServices;

namespace Backend.Extensions;

internal static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        internal static T ThrowIfNotOfType<T>(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            return argument is T value
                ? value
                : throw new ArgumentException(
                    $"Expected value of type {typeof(T).FullName} but found {argument?.ToString() ?? "Null"}", paramName);
        }
    }
}