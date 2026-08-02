using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Extensions;

namespace Backend.Services;

public sealed class SocketRegistryService
{
    private HashSet<Socket> _sockets = new();

    internal event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
    internal event AsyncEventHandler<SocketCloseEventArgs>? Close;
    
    internal void RegisterSocket(Socket socket)
    {
        _sockets.Add(socket);
        socket.MessageReceived += SocketOnMessageReceived;
        socket.Close += SocketOnClose;
    }

    private async Task SocketOnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (MessageReceived is { } handler) await handler(sender, e);
    }

    private async Task SocketOnClose(object? sender, SocketCloseEventArgs e)
    {
        if (Close is { } handler) await handler(sender, e);
        
        Socket socket = ArgumentException.ThrowIfNotOfType<Socket>(sender);
        _sockets.Remove(socket);
        socket.MessageReceived -= SocketOnMessageReceived;
        socket.Close -= SocketOnClose;
    }
}