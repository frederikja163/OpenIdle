using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backend.Dtos;

namespace Backend;

public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);

internal sealed class MessageReceivedEventArgs(DtoBase dto) : EventArgs
{
    public DtoBase Dto { get; init; } = dto;
}

internal sealed class SocketCloseEventArgs : EventArgs
{
    
}

internal sealed class Socket : IDisposable
{
    private readonly WebSocket _webSocket;
    private bool _isClosed = false;
    
    internal Socket(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    internal WebSocketState State => _webSocket.State;

    internal event AsyncEventHandler<MessageReceivedEventArgs>? MessageReceived;
    internal event AsyncEventHandler<SocketCloseEventArgs>? Close;

    internal async Task StartAsync()
    {
        try
        {
            while (!_isClosed)
            {
                byte[] bytes = new byte[1024];
                WebSocketReceiveResult receiveResult = await _webSocket.ReceiveAsync(bytes, CancellationToken.None);
                if (!receiveResult.EndOfMessage)
                {
                    throw new NotImplementedException("Need to implement support for messages bigger than 1KiB");
                }

                switch (receiveResult.MessageType)
                {
                    case WebSocketMessageType.Text:
                        string str = Encoding.UTF8.GetString(bytes.AsSpan(0, receiveResult.Count));
                        DtoBase dto = JsonSerializer.Deserialize<DtoBase>(str) ??
                                      throw new FormatException(
                                          "Payload was either malformed json or an unrecognized json object.");
                        if (MessageReceived is { } handler) await handler(this, new MessageReceivedEventArgs(dto));
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
        catch (Exception exception)
        {
            await CloseAsync(WebSocketCloseStatus.InternalServerError, exception.Message);
        }
    }

    internal async Task SendMessageAsync(DtoBase dtoBase, CancellationToken cancellationToken = default)
    {
        string str = JsonSerializer.Serialize(dtoBase, typeof(DtoBase));
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    internal async Task CloseAsync(WebSocketCloseStatus status, string description)
    {
        if (_isClosed)
        {
            return;
        }
        _isClosed = true;
        await _webSocket.CloseAsync(status, description, CancellationToken.None);
        if (Close is { } handler) await handler(this, new SocketCloseEventArgs());
    }

    public void Dispose()
    {
        _webSocket.Dispose();
    }
}