using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Backend.Dtos;
using Backend.Entities;
using Backend.Extensions;

namespace Backend;

internal sealed class MessageReceivedEventArgs(RequestBase request) : EventArgs
{
    public RequestBase Request { get; init; } = request;
}

internal sealed class SocketCloseEventArgs : EventArgs
{
    
}

internal sealed class Socket : IDisposable
{
    private readonly WebSocket _webSocket;
    private bool _isClosed = false;
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
    
    internal Socket(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    internal WebSocketState State => _webSocket.State;
    
    internal User? User { get; set; }
    internal Profile? Profile { get; set; }

    internal event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
    internal event AsyncEventHandler<SocketCloseEventArgs>? Close;

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[1024];
        try
        {
            while (!_isClosed)
            {
                WebSocketReceiveResult receiveResult = await _webSocket.ReceiveAsync(bytes, cancellationToken);

                if (!receiveResult.EndOfMessage)
                {
                    throw new NotImplementedException("Need to implement support for messages bigger than 1KiB");
                }

                switch (receiveResult.MessageType)
                {
                    case WebSocketMessageType.Text:
                        await HandleTextMessageAsync(bytes, receiveResult.Count);
                        break;
                    case WebSocketMessageType.Binary:
                        throw new NotSupportedException("Binary messages are not supported.");
                    case WebSocketMessageType.Close:
                        await CloseAsync(receiveResult.CloseStatus.GetValueOrDefault(),
                            receiveResult.CloseStatusDescription!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        catch (OperationCanceledException)
        {
            await CloseAsync(WebSocketCloseStatus.NormalClosure, "Cancellation was requested");
        }
        catch (Exception exception)
        {
            await CloseAsync(WebSocketCloseStatus.InternalServerError, exception.Message);
        }
    }

    internal async Task SendResponseAsync(ResponseBase response)
    {
        await SendMessageAsync(SocketJsonSerializer.Serialize(response));
    }

    internal async Task SendEventAsync(EventBase eventBase)
    {
        await SendMessageAsync(SocketJsonSerializer.Serialize(eventBase));
    }

    internal async Task SendMessageAsync(byte[] bytes)
    {
        await _sendLock.WaitAsync();
        try
        {
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task HandleTextMessageAsync(byte[] bytes, int count)
    {
        try
        {
            RequestBase dto = SocketJsonSerializer.DeserializeRequest(bytes, count);
            if (MessageReceived is { } handler) await handler(this, new MessageReceivedEventArgs(dto));
        }
        catch (Exception exception)
        {
            await SendResponseAsync(new ErrorResponse(exception.Message));
        }
    }

    internal async Task CloseAsync(WebSocketCloseStatus status, string description)
    {
        if (_isClosed)
        {
            return;
        }
        _isClosed = true;

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        try
        {
            await _webSocket.CloseAsync(status, description, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _webSocket.Abort();
        }

        if (Close is { } handler) await handler(this, new SocketCloseEventArgs());
    }

    public void Dispose()
    {
        _webSocket.Dispose();
    }
}