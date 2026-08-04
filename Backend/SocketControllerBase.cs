using System;
using System.Threading.Tasks;
using Backend.Dtos;
using Backend.Entities;
using Backend.Services;

namespace Backend;

internal record SocketControllerContext(Socket Socket, RequestBase Request, SocketRegistryService SocketRegistry);

public abstract class SocketControllerBase
{
    internal Socket Socket => Context.Socket;
    internal User? User => Socket.User;
    internal Profile? Profile => Socket.Profile;
    internal RequestBase Request => Context.Request;
    internal SocketRegistryService SocketRegistry => Context.SocketRegistry;

    internal User UserOrThrow
    {
        get
        {
            field ??= Socket.User ?? throw new InvalidOperationException("Socket is not signed in.");
            return field;
        }
    }

    internal Profile ProfileOrThrow
    {
        get
        {
            field ??= Socket.Profile ?? throw new InvalidOperationException("Socket has no selected profile.");
            return field;
        }
    }

    internal SocketControllerContext Context
    {
        get => field;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = null!;

    public async Task RespondAsync(ResponseBase response)
    {
        response.Id = Request.Id;
        await Socket.SendResponseAsync(response);
    }

    public async Task SendProfileEventAsync(EventBase eventBase)
    {
        await SocketRegistry.SendToProfileAsync(ProfileOrThrow, eventBase);
    }

    public async Task SendUserEventAsync(EventBase eventBase)
    {
        await SocketRegistry.SendToUserAsync(UserOrThrow, eventBase);
    }
}