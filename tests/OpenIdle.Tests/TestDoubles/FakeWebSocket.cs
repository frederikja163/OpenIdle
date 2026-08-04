using System.Net.WebSockets;
using System.Text;

namespace OpenIdle.Tests.TestDoubles;

public sealed class FakeWebSocket : WebSocket
{
    private readonly WebSocketState _state;
    private readonly List<(byte[] Bytes, WebSocketMessageType Type)> _sent = [];
    private int _sendFailuresLeft;
    private WebSocketCloseStatus? _closeStatus;
    private string? _closeStatusDescription;

    public FakeWebSocket(WebSocketState state = WebSocketState.Open)
    {
        _state = state;
    }

    public IReadOnlyList<(byte[] Bytes, WebSocketMessageType Type)> Sent => _sent;

    public string? FirstSentText => _sent.Count == 0 ? null : Encoding.UTF8.GetString(_sent[0].Bytes);

    public int SendAttempts => _sent.Count;

    public void ThrowOnNextSend()
    {
        Interlocked.Increment(ref _sendFailuresLeft);
    }

    public override WebSocketState State => _state;

    public override string? SubProtocol => null;

    public override WebSocketCloseStatus? CloseStatus => _closeStatus;

    public override string? CloseStatusDescription => _closeStatusDescription;

    public override void Abort()
    {
    }

    public override void Dispose()
    {
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _closeStatusDescription = statusDescription;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _closeStatusDescription = statusDescription;
        return Task.CompletedTask;
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("ReceiveAsync is not used by these tests.");
    }

    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("ReceiveAsync is not used by these tests.");
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
        CancellationToken cancellationToken)
    {
        RecordSend(buffer.ToArray(), messageType);
        return Task.CompletedTask;
    }

    public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken)
    {
        RecordSend(buffer.ToArray(), messageType);
        return ValueTask.CompletedTask;
    }

    private void RecordSend(byte[] bytes, WebSocketMessageType messageType)
    {
        if (Interlocked.Decrement(ref _sendFailuresLeft) >= 0)
        {
            throw new InvalidOperationException("Simulated send failure.");
        }

        lock (_sent)
        {
            _sent.Add((bytes, messageType));
        }
    }
}
