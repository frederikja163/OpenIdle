using System;
using System.Threading.Tasks;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;

namespace Backend;

internal record SocketControllerContext(Socket Socket, RequestBase Request, SocketRegistryService SocketRegistry);

public abstract class SocketControllerBase
{
    internal Socket Socket => Context.Socket;
    internal RequestBase Request => Context.Request;
    internal SocketRegistryService SocketRegistry => Context.SocketRegistry;

    internal User UserOrThrow
    {
        get
        {
            field ??= Socket.User ?? throw new BackendException("You are not signed in.");
            return field;
        }
    }

    internal Profile ProfileOrThrow
    {
        get
        {
            field ??= Socket.Profile ?? throw new BackendException("You must select a profile first.");
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
        response.RequestId = Request.RequestId;
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