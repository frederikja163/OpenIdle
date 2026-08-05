using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backend.Dtos;
using Backend.Entities;
using Backend.Errors;

namespace Backend;

public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);

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
        await SendMessageAsync(response);
    }

    internal async Task SendEventAsync(EventBase eventBase)
    {
        await SendMessageAsync(eventBase);
    }

    private async Task HandleTextMessageAsync(byte[] bytes, int count)
    {
        try
        {
            string str = Encoding.UTF8.GetString(bytes.AsSpan(0, count));
            RequestBase dto = (JsonSerializer.Deserialize<DtoBase>(str) as RequestBase) ??
                              throw new FormatException(
                                  "Payload was either malformed json or an unrecognized json object.");
            if (MessageReceived is { } handler) await handler(this, new MessageReceivedEventArgs(dto));
        }
        catch (Exception exception)
        {
            await SendResponseAsync(new ErrorResponse(
                exception is ErrorCodeException errorCode ? errorCode.Code : null,
                exception.Message));
        }
    }

    private async Task SendMessageAsync(DtoBase dtoBase)
    {
        string str = JsonSerializer.Serialize(dtoBase, typeof(DtoBase));
        byte[] bytes = Encoding.UTF8.GetBytes(str);
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