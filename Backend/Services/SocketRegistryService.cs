using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Backend.Dtos;
using Backend.Entities;
using Backend.Extensions;
using Microsoft.Extensions.Logging;

namespace Backend.Services;

public sealed class SocketRegistryService
{
    private readonly ILogger<SocketRegistryService> _logger;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Socket, byte>> _socketsByProfile = new();
    private readonly ConcurrentDictionary<Socket, Guid> _profileBySocket = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Socket, byte>> _socketsByUser = new();
    private readonly ConcurrentDictionary<Socket, Guid> _userBySocket = new();

    internal event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
    internal event AsyncEventHandler<SocketCloseEventArgs>? Close;

    public SocketRegistryService(ILogger<SocketRegistryService> logger)
    {
        _logger = logger;
    }

    internal void RegisterSocket(Socket socket)
    {
        socket.MessageReceived += SocketOnMessageReceived;
        socket.Close += SocketOnClose;
    }

    internal void SetProfile(Socket socket, Profile profile)
    {
        if (_profileBySocket.TryRemove(socket, out Guid previousProfileId) &&
            _socketsByProfile.TryGetValue(previousProfileId, out ConcurrentDictionary<Socket, byte>? previousSockets))
        {
            previousSockets.TryRemove(socket, out _);
        }

        _profileBySocket[socket] = profile.ProfileId;
        ConcurrentDictionary<Socket, byte> sockets = _socketsByProfile.GetOrAdd(profile.ProfileId, _ => new());
        sockets[socket] = 0;
    }

    internal void SetUser(Socket socket, User user)
    {
        if (_userBySocket.TryRemove(socket, out Guid previousUserId) &&
            _socketsByUser.TryGetValue(previousUserId, out ConcurrentDictionary<Socket, byte>? previousSockets))
        {
            previousSockets.TryRemove(socket, out _);
        }

        _userBySocket[socket] = user.UserId;
        ConcurrentDictionary<Socket, byte> sockets = _socketsByUser.GetOrAdd(user.UserId, _ => new());
        sockets[socket] = 0;
    }

    internal async Task SendToProfileAsync(Profile profile, EventBase eventBase)
    {
        if (!_socketsByProfile.TryGetValue(profile.ProfileId, out ConcurrentDictionary<Socket, byte>? sockets))
        {
            return;
        }

        byte[] bytes = SocketJsonSerializer.Serialize(eventBase);
        foreach (Socket socket in sockets.Keys.ToArray())
        {
            try
            {
                await socket.SendMessageAsync(bytes);
            }
            catch (Exception exception) when (IsTransportException(exception))
            {
                _logger.LogError(exception, "Failed to send event {EventType} to socket.", eventBase.GetType().Name);
                RemoveSocket(socket);
            }
        }
    }

    internal async Task SendToUserAsync(User user, EventBase eventBase)
    {
        if (!_socketsByUser.TryGetValue(user.UserId, out ConcurrentDictionary<Socket, byte>? sockets))
        {
            return;
        }

        byte[] bytes = SocketJsonSerializer.Serialize(eventBase);
        foreach (Socket socket in sockets.Keys.ToArray())
        {
            try
            {
                await socket.SendMessageAsync(bytes);
            }
            catch (Exception exception) when (IsTransportException(exception))
            {
                _logger.LogError(exception, "Failed to send event {EventType} to socket.", eventBase.GetType().Name);
                RemoveSocket(socket);
            }
        }
    }

    private static bool IsTransportException(Exception exception)
    {
        return exception is WebSocketException or IOException or ObjectDisposedException;
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
            RemoveSocket(socket);
            socket.MessageReceived -= SocketOnMessageReceived;
            socket.Close -= SocketOnClose;
        }
    }

    private void RemoveSocket(Socket socket)
    {
        if (_profileBySocket.TryRemove(socket, out Guid profileId) &&
            _socketsByProfile.TryGetValue(profileId, out ConcurrentDictionary<Socket, byte>? profileSockets))
        {
            profileSockets.TryRemove(socket, out _);
        }

        if (_userBySocket.TryRemove(socket, out Guid userId) &&
            _socketsByUser.TryGetValue(userId, out ConcurrentDictionary<Socket, byte>? userSockets))
        {
            userSockets.TryRemove(socket, out _);
        }
    }
}
