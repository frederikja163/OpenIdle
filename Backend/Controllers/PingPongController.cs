using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Dtos;

namespace Backend.Controllers;

[SocketController]
public sealed class PingPongController : SocketControllerBase
{
    [Request]
    public async Task Ping(PingRequest request)
    {
        await RespondAsync(new PongResponse());
    }
}