using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Backend.Dtos;
using Backend.Extensions;

namespace Backend.Services;

public sealed class SocketRegistryService
{
    private readonly ConcurrentDictionary<ProfileId, ConcurrentDictionary<Socket, byte>> _socketsByProfile = new();
    private readonly ConcurrentDictionary<Socket, ProfileId> _profileBySocket = new();
    private readonly ConcurrentDictionary<UserId, ConcurrentDictionary<Socket, byte>> _socketsByUser = new();
    private readonly ConcurrentDictionary<Socket, UserId> _userBySocket = new();

    internal event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
    internal event AsyncEventHandler<SocketCloseEventArgs>? Close;
    internal event AsyncEventHandler<ProfileOnlineEventArgs>? ProfileOnline;
    internal event AsyncEventHandler<ProfileOfflineEventArgs>? ProfileOffline;

    internal void RegisterSocket(Socket socket)
    {
        socket.MessageReceived += SocketOnMessageReceived;
        socket.Close += SocketOnClose;
    }

    internal async Task SetProfile(Socket socket, ProfileId profileId)
    {
        if (_profileBySocket.TryGetValue(socket, out ProfileId currentProfileId) && currentProfileId == profileId)
        {
            return;
        }

        if (_profileBySocket.TryRemove(socket, out ProfileId previousProfileId) &&
            RemoveSocketFromProfile(socket, previousProfileId))
        {
            await NotifyProfileOffline(previousProfileId);
        }

        _profileBySocket[socket] = profileId;
        if (AddSocketToProfile(socket, profileId))
        {
            Log.Debug($"Profile {profileId} came online.");
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

        foreach (Socket socket in sockets.Keys.ToArray())
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            try
            {
                await socket.SendEventAsync(eventBase);
            }
            catch (Exception exception) when (IsTransportException(exception))
            {
                Log.Error(exception);
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

        foreach (Socket socket in sockets.Keys.ToArray())
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            try
            {
                await socket.SendEventAsync(eventBase);
            }
            catch (Exception exception) when (IsTransportException(exception))
            {
                Log.Error(exception);
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
            RemoveSocketFromProfile(socket, profileId))
        {
            await NotifyProfileOffline(profileId);
        }

        if (_userBySocket.TryRemove(socket, out UserId userId) &&
            _socketsByUser.TryGetValue(userId, out ConcurrentDictionary<Socket, byte>? userSockets))
        {
            userSockets.TryRemove(socket, out _);
        }
    }

    private bool AddSocketToProfile(Socket socket, ProfileId profileId)
    {
        ConcurrentDictionary<Socket, byte> sockets = _socketsByProfile.GetOrAdd(profileId, _ => new());
        lock (sockets)
        {
            bool becameOnline = sockets.IsEmpty;
            sockets[socket] = 0;
            return becameOnline;
        }
    }

    private bool RemoveSocketFromProfile(Socket socket, ProfileId profileId)
    {
        if (!_socketsByProfile.TryGetValue(profileId, out ConcurrentDictionary<Socket, byte>? sockets))
        {
            return false;
        }

        lock (sockets)
        {
            return sockets.TryRemove(socket, out _) && sockets.IsEmpty;
        }
    }

    private async Task NotifyProfileOffline(ProfileId profileId)
    {
        Log.Debug($"Profile {profileId} went offline.");
        if (ProfileOffline is { } offlineHandlers)
        {
            await offlineHandlers.InvokeAsync(this, new ProfileOfflineEventArgs(profileId));
        }
    }
}
