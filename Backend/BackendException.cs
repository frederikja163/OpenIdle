using System;
using Backend.Database.Entities;

namespace Backend;

internal sealed class BackendException : Exception
{
    internal BackendException(string message)
    {
        Message = message;
    }
    
    internal string Message { get; private init; }
}