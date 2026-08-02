using System;

namespace Backend;

internal record SocketControllerContext(Socket Socket);

public abstract class SocketControllerBase
{
    internal Socket Socket => Context.Socket;

    internal SocketControllerContext Context
    {
        get => field;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = null!;
}