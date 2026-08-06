using System;
using System.Threading.Tasks;
using Backend.Database.Entities;
using Backend.Dtos;

namespace Backend;

internal record SocketControllerContext(Socket Socket, RequestBase Request);

public abstract class SocketControllerBase
{
    internal Socket Socket => Context.Socket;
    internal User? User => Socket.User;
    internal Profile? Profile => Socket.Profile;
    internal RequestBase Request => Context.Request;

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
}