using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Extensions;

namespace Backend.Services;

public sealed class SocketRegistryService
{
    internal event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
    internal event AsyncEventHandler<SocketCloseEventArgs>? Close;
    
    internal void RegisterSocket(Socket socket)
    {
        socket.MessageReceived += SocketOnMessageReceived;
        socket.Close += SocketOnClose;
    }

    private async Task SocketOnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (MessageReceived is { } handler) await handler(sender, e);
    }

    private async Task SocketOnClose(object? sender, SocketCloseEventArgs e)
    {
        try
        {
            if (Close is { } handler) await handler(sender, e);
        }
        finally
        {
            Socket socket = ArgumentException.ThrowIfNotOfType<Socket>(sender);
            socket.MessageReceived -= SocketOnMessageReceived;
            socket.Close -= SocketOnClose;
        }
    }
}