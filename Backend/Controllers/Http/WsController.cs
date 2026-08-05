using System.Net.WebSockets;
using System.Threading.Tasks;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Http;

[ApiController]
public sealed class WsController(SocketRegistryService socketRegistryService) : ControllerBase
{
    // TODO: According to claude this needs an origin check?    
    [HttpGet("/ws")]
    public async Task Ws()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("Expected a WebSocket request to /ws");
            return;
        }

        using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        Socket socket = new(webSocket);
        socketRegistryService.RegisterSocket(socket);
        await socket.StartAsync(HttpContext.RequestAborted);
    }
}
