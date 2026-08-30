using System;
using System.Threading.Tasks;
using Backend.Dtos;
using Backend.Services;

namespace Backend;

internal record SocketControllerContext(Socket Socket, RequestBase Request, SocketRegistryService SocketRegistry);

public abstract class SocketControllerBase
{
    internal Socket Socket => Context.Socket;
    internal RequestBase Request => Context.Request;
    internal SocketRegistryService SocketRegistry => Context.SocketRegistry;

    internal UserId UserId
    {
        get
        {
            UserId? userId = Socket.UserId;
            if (userId is null)
            {
                throw new BackendException("You are not signed in.");
            }
            return userId.Value;
        }
    }

    internal ProfileId ProfileId
    {
        get
        {
            ProfileId? profileId = Socket.ProfileId;
            if (profileId is null)
            {
                throw new BackendException("You must select a profile first.");
            }
            return profileId.Value;
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
        await SocketRegistry.SendToProfileAsync(ProfileId, eventBase);
    }

    public async Task SendUserEventAsync(EventBase eventBase)
    {
        await SocketRegistry.SendToUserAsync(UserId, eventBase);
    }
}