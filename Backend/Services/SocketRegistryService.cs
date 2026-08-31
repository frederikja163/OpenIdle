using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Backend.Dtos;
using Backend.Extensions;
using Microsoft.Extensions.Logging;

namespace Backend.Services;

public sealed class SocketRegistryService
{
    private readonly ILogger<SocketRegistryService> _logger;
    private readonly ConcurrentDictionary<ProfileId, ConcurrentDictionary<Socket, byte>> _socketsByProfile = new();
    private readonly ConcurrentDictionary<Socket, ProfileId> _profileBySocket = new();
    private readonly ConcurrentDictionary<UserId, ConcurrentDictionary<Socket, byte>> _socketsByUser = new();
    private readonly ConcurrentDictionary<Socket, UserId> _userBySocket = new();

    internal event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
    internal event AsyncEventHandler<SocketCloseEventArgs>? Close;
    internal event AsyncEventHandler<ProfileOnlineEventArgs>? ProfileOnline;
    internal event AsyncEventHandler<ProfileOfflineEventArgs>? ProfileOffline;

    public SocketRegistryService(ILogger<SocketRegistryService> logger)
    {
        _logger = logger;
    }

    internal void RegisterSocket(Socket socket)
    {
        socket.MessageReceived += SocketOnMessageReceived;
        socket.Close += SocketOnClose;
    }

    internal async Task SetProfile(Socket socket, ProfileId profileId)
    {
        if (_profileBySocket.TryRemove(socket, out ProfileId previousProfileId) &&
            _socketsByProfile.TryGetValue(previousProfileId, out ConcurrentDictionary<Socket, byte>? previousSockets))
        {
            previousSockets.TryRemove(socket, out _);
        }

        _profileBySocket[socket] = profileId;
        ConcurrentDictionary<Socket, byte> sockets = _socketsByProfile.GetOrAdd(profileId, _ => new());
        bool becameOnline = sockets.IsEmpty;
        sockets[socket] = 0;
        if (becameOnline)
        {
            _logger.LogInformation("Profile {ProfileId} came online.", profileId);
            if (ProfileOnline is { } onlineHandlers)
            {
                await onlineHandlers.InvokeAsync(this, new ProfileOnlineEventArgs(profileId));
            }
        }
    }

    internal void SetUser(Socket socket, UserId userId)
    {
        if (_userBySocket.TryRemove(socket, out UserId previousUserId) &&
            _socketsByUser.TryGetValue(previousUserId, out ConcurrentDictionary<Socket, byte>? previousSockets))
        {
            previousSockets.TryRemove(socket, out _);
        }

        _userBySocket[socket] = userId;
        ConcurrentDictionary<Socket, byte> sockets = _socketsByUser.GetOrAdd(userId, _ => new());
        sockets[socket] = 0;
    }

    internal async Task SendToProfileAsync(ProfileId profileId, EventBase eventBase)
    {
        if (!_socketsByProfile.TryGetValue(profileId, out ConcurrentDictionary<Socket, byte>? sockets))
        {
            return;
        }

        byte[] bytes = SocketJsonSerializer.Serialize(eventBase);
        foreach (Socket socket in sockets.Keys.ToArray())
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            try
            {
                await socket.SendMessageAsync(bytes, timeout.Token);
            }
            catch (Exception exception) when (IsTransportException(exception))
            {
                _logger.LogError(exception, "Failed to send event {EventType} to socket.", eventBase.GetType().Name);
                await RemoveSocket(socket);
            }
        }
    }

    internal async Task SendToUserAsync(UserId userId, EventBase eventBase)
    {
        if (!_socketsByUser.TryGetValue(userId, out ConcurrentDictionary<Socket, byte>? sockets))
        {
            return;
        }

        byte[] bytes = SocketJsonSerializer.Serialize(eventBase);
        foreach (Socket socket in sockets.Keys.ToArray())
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            try
            {
                await socket.SendMessageAsync(bytes, timeout.Token);
            }
            catch (Exception exception) when (IsTransportException(exception))
            {
                _logger.LogError(exception, "Failed to send event {EventType} to socket.", eventBase.GetType().Name);
                await RemoveSocket(socket);
            }
        }
    }

    private static bool IsTransportException(Exception exception)
    {
        return exception is WebSocketException or IOException or ObjectDisposedException or OperationCanceledException;
    }

    private async Task SocketOnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (MessageReceived is { } handler) await handler.InvokeAsync(sender, e);
    }

    private async Task SocketOnClose(object? sender, SocketCloseEventArgs e)
    {
        try
        {
            await Close.InvokeAsync(sender, e);
        }
        finally
        {
            Socket socket = ArgumentException.ThrowIfNotOfType<Socket>(sender);
            await RemoveSocket(socket);
            socket.MessageReceived -= SocketOnMessageReceived;
            socket.Close -= SocketOnClose;
        }
    }

    private async Task RemoveSocket(Socket socket)
    {
        if (_profileBySocket.TryRemove(socket, out ProfileId profileId) &&
            _socketsByProfile.TryGetValue(profileId, out ConcurrentDictionary<Socket, byte>? profileSockets))
        {
            profileSockets.TryRemove(socket, out _);
            if (profileSockets.IsEmpty)
            {
                _logger.LogInformation("Profile {ProfileId} went offline.", profileId);
                if (ProfileOffline is { } offlineHandlers)
                {
                    await offlineHandlers.InvokeAsync(this, new ProfileOfflineEventArgs(profileId));
                }
            }
        }

        if (_userBySocket.TryRemove(socket, out UserId userId) &&
            _socketsByUser.TryGetValue(userId, out ConcurrentDictionary<Socket, byte>? userSockets))
        {
            userSockets.TryRemove(socket, out _);
        }
    }
}
