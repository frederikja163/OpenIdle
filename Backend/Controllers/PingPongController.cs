using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Dtos;

namespace Backend.Controllers;

[SocketController]
public sealed class PingPongController : SocketControllerBase
{
    [Request]
    public Task Ping(PingRequest request)
    {
        return Socket.SendResponse(new PongResponse());
    }
}