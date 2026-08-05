using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Backend;
using Backend.Dtos;

namespace OpenIdle.IntegrationTests;

public sealed class TestSocketClient : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly ClientWebSocket webSocket;

    private TestSocketClient(ClientWebSocket webSocket)
    {
        this.webSocket = webSocket;
    }

    public static async Task<TestSocketClient> ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        ClientWebSocket webSocket = new();
        await webSocket.ConnectAsync(uri, timeout.Token);
        return new TestSocketClient(webSocket);
    }

    public async Task SendAsync(RequestBase request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        byte[] payload = SocketJsonSerializer.Serialize(request);
        await webSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
    }

    public async Task SendRawAsync(string json, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        byte[] payload = System.Text.Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
    }

    public async Task<DtoBase> ReceiveAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        System.IO.MemoryStream stream = new();

        WebSocketReceiveResult result;
        do
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            result = await webSocket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closed by peer", cancellationToken);
                throw new InvalidOperationException("Peer closed the connection before sending a response.");
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return SocketJsonSerializer.Deserialize(stream.ToArray(), (int)stream.Length);
    }

    public async Task<DtoBase> ReceiveUntilAsync(Func<DtoBase, bool> predicate, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        DtoBase response;
        do
        {
            response = await ReceiveAsync(timeout.Token);
        }
        while (!predicate(response));

        return response;
    }

    public void Dispose()
    {
        webSocket.Dispose();
    }
}
