using System;
using System.Collections.Generic;
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
    private readonly object _sync = new();
    private readonly Dictionary<ProfileId, HashSet<Socket>> _socketsByProfile = new();
    private readonly Dictionary<Socket, ProfileId> _profileBySocket = new();
    private readonly Dictionary<UserId, HashSet<Socket>> _socketsByUser = new();
    private readonly Dictionary<Socket, UserId> _userBySocket = new();

    internal event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
    internal event AsyncEventHandler<SocketCloseEventArgs>? Close;
    internal event AsyncEventHandler<ProfileOnlineEventArgs>? ProfileOnline;
    internal event AsyncEventHandler<ProfileOfflineEventArgs>? ProfileOffline;

    internal bool IsProfileOnline(ProfileId profileId)
    {
        lock (_sync)
        {
            return _socketsByProfile.TryGetValue(profileId, out var sockets) && sockets.Count > 0;
        }
    }

    internal bool IsUserOnline(UserId userId)
    {
        lock (_sync)
        {
            return _socketsByUser.TryGetValue(userId, out var sockets) && sockets.Count > 0;
        }
    }
    
    internal void RegisterSocket(Socket socket)
    {
        socket.MessageReceived += SocketOnMessageReceived;
        socket.Close += SocketOnClose;
    }

    internal async Task SetProfile(Socket socket, ProfileId profileId)
    {
        ProfileId? offlineProfileId = null;
        ProfileId? onlineProfileId = null;

        lock (_sync)
        {
            if (_profileBySocket.TryGetValue(socket, out ProfileId currentProfileId) && currentProfileId == profileId)
            {
                return;
            }

            if (_profileBySocket.Remove(socket, out ProfileId previousProfileId) &&
                RemoveSocketFromProfile(socket, previousProfileId))
            {
                offlineProfileId = previousProfileId;
            }

            _profileBySocket[socket] = profileId;
            if (AddSocketToProfile(socket, profileId))
            {
                onlineProfileId = profileId;
            }
        }

        if (offlineProfileId is { } previousProfile)
        {
            await NotifyProfileOffline(previousProfile);
        }

        if (onlineProfileId is { } currentProfile)
        {
            await NotifyProfileOnline(currentProfile);
        }
    }

    internal void SetUser(Socket socket, UserId userId)
    {
        lock (_sync)
        {
            if (_userBySocket.Remove(socket, out UserId previousUserId) &&
                _socketsByUser.TryGetValue(previousUserId, out HashSet<Socket>? previousSockets))
            {
                previousSockets.Remove(socket);
                if (previousSockets.Count == 0)
                {
                    _socketsByUser.Remove(previousUserId);
                }
            }

            _userBySocket[socket] = userId;
            if (!_socketsByUser.TryGetValue(userId, out HashSet<Socket>? sockets))
            {
                sockets = new HashSet<Socket>();
                _socketsByUser[userId] = sockets;
            }
            sockets.Add(socket);
        }
    }

    internal async Task SendToProfileAsync(ProfileId profileId, EventBase eventBase)
    {
        Socket[] snapshot;
        lock (_sync)
        {
            if (!_socketsByProfile.TryGetValue(profileId, out HashSet<Socket>? sockets))
            {
                return;
            }

            snapshot = sockets.ToArray();
        }

        foreach (Socket socket in snapshot)
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
        Socket[] snapshot;
        lock (_sync)
        {
            if (!_socketsByUser.TryGetValue(userId, out HashSet<Socket>? sockets))
            {
                return;
            }

            snapshot = sockets.ToArray();
        }

        foreach (Socket socket in snapshot)
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
        ProfileId? offlineProfileId = null;
        lock (_sync)
        {
            if (_profileBySocket.Remove(socket, out ProfileId profileId) &&
                RemoveSocketFromProfile(socket, profileId))
            {
                offlineProfileId = profileId;
            }

            if (_userBySocket.Remove(socket, out UserId userId) &&
                _socketsByUser.TryGetValue(userId, out HashSet<Socket>? userSockets))
            {
                userSockets.Remove(socket);
                if (userSockets.Count == 0)
                {
                    _socketsByUser.Remove(userId);
                }
            }
        }

        if (offlineProfileId is { } offlineProfile)
        {
            await NotifyProfileOffline(offlineProfile);
        }
    }

    private bool AddSocketToProfile(Socket socket, ProfileId profileId)
    {
        if (!_socketsByProfile.TryGetValue(profileId, out HashSet<Socket>? sockets))
        {
            sockets = new HashSet<Socket>();
            _socketsByProfile[profileId] = sockets;
        }

        bool becameOnline = sockets.Count == 0;
        sockets.Add(socket);
        return becameOnline;
    }

    private bool RemoveSocketFromProfile(Socket socket, ProfileId profileId)
    {
        if (!_socketsByProfile.TryGetValue(profileId, out HashSet<Socket>? sockets))
        {
            return false;
        }

        bool removed = sockets.Remove(socket);
        if (sockets.Count == 0)
        {
            _socketsByProfile.Remove(profileId);
        }

        return removed && sockets.Count == 0;
    }

    private async Task NotifyProfileOffline(ProfileId profileId)
    {
        Log.Debug($"Profile {profileId} went offline.");
        if (ProfileOffline is { } offlineHandlers)
        {
            await offlineHandlers.InvokeAsync(this, new ProfileOfflineEventArgs(profileId));
        }
    }

    private async Task NotifyProfileOnline(ProfileId profileId)
    {
        Log.Debug($"Profile {profileId} came online.");
        if (ProfileOnline is { } onlineHandlers)
        {
            await onlineHandlers.InvokeAsync(this, new ProfileOnlineEventArgs(profileId));
        }
    }
}
