using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;

namespace OpenIdle.IntegrationTests;

public sealed class TestSocket : IDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly Uri _uri;
    private readonly TimeSpan _timeout;

    public TestSocket(Uri wsUri, TimeSpan? timeout = null)
    {
        _uri = wsUri;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task ConnectAsync(CancellationToken externalToken = default)
    {
        using CancellationTokenSource cts = CreateTimeoutTokenSource(externalToken);
        await _socket.ConnectAsync(_uri, cts.Token).ConfigureAwait(false);
    }

    public async Task SendAsync(string json, CancellationToken externalToken = default)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using CancellationTokenSource cts = CreateTimeoutTokenSource(externalToken);
        await _socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, cts.Token).ConfigureAwait(false);
    }

    public async Task<string> ReceiveAsync(CancellationToken externalToken = default)
    {
        using CancellationTokenSource cts = CreateTimeoutTokenSource(externalToken);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[4096];
        ValueWebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(chunk.AsMemory(), cts.Token).ConfigureAwait(false);
            buffer.Write(chunk, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private CancellationTokenSource CreateTimeoutTokenSource(CancellationToken externalToken)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        cts.CancelAfter(_timeout);
        return cts;
    }

    public void Dispose() => _socket.Dispose();
}
