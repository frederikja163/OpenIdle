using System.Net.WebSockets;
using Backend;
using Backend.Dtos.Auth;
using Backend.Entities;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdle.Tests.TestDoubles;

namespace OpenIdle.Tests.Services;

public sealed class SocketRegistryServiceTests
{
    private static readonly User UserA = new() { UserId = Guid.NewGuid() };
    private static readonly User UserB = new() { UserId = Guid.NewGuid() };

    private static readonly Profile ProfileA = new() { ProfileId = Guid.NewGuid(), Name = "A" };
    private static readonly Profile ProfileB = new() { ProfileId = Guid.NewGuid(), Name = "B" };

    [Fact]
    public async Task SendToUserAsync_DeliversEventToAllSocketsOfUser()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket first = CreateRegisteredSocket(registry);
        TestSocket second = CreateRegisteredSocket(registry);
        TestSocket third = CreateRegisteredSocket(registry);
        registry.SetUser(first.Socket, UserA);
        registry.SetUser(second.Socket, UserA);
        registry.SetUser(third.Socket, UserB);

        await registry.SendToUserAsync(UserA, CreateEvent());

        AssertSingleEventSent(first);
        AssertSingleEventSent(second);
        AssertNoEventSent(third);
    }

    [Fact]
    public async Task SendToUserAsync_MovedSocket_LeavesPreviousUserBucket()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetUser(testSocket.Socket, UserA);

        registry.SetUser(testSocket.Socket, UserB);

        await registry.SendToUserAsync(UserA, CreateEvent());
        await registry.SendToUserAsync(UserB, CreateEvent());
        Assert.Equal(1, testSocket.WebSocket.SendAttempts);
        Assert.Contains("ProfilesChangedEvent", testSocket.WebSocket.FirstSentText);
    }

    [Fact]
    public async Task SendToProfileAsync_DeliversEventToAllSocketsOfProfile()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket first = CreateRegisteredSocket(registry);
        TestSocket second = CreateRegisteredSocket(registry);
        registry.SetUser(first.Socket, UserA);
        registry.SetUser(second.Socket, UserA);
        registry.SetProfile(first.Socket, ProfileA);
        registry.SetProfile(second.Socket, ProfileB);

        await registry.SendToProfileAsync(ProfileA, CreateEvent());

        AssertSingleEventSent(first);
        AssertNoEventSent(second);
    }

    [Fact]
    public async Task SendToProfileAsync_MovedSocket_LeavesPreviousProfileBucket()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetProfile(testSocket.Socket, ProfileA);

        registry.SetProfile(testSocket.Socket, ProfileB);

        await registry.SendToProfileAsync(ProfileA, CreateEvent());
        await registry.SendToProfileAsync(ProfileB, CreateEvent());
        Assert.Equal(1, testSocket.WebSocket.SendAttempts);
        Assert.Contains("ProfilesChangedEvent", testSocket.WebSocket.FirstSentText);
    }

    [Fact]
    public async Task SendToUserAsync_WithNoSockets_DoesNotThrow()
    {
        SocketRegistryService registry = CreateRegistry();

        await registry.SendToUserAsync(UserA, CreateEvent());
    }

    [Fact]
    public async Task SendToProfileAsync_WithNoSockets_DoesNotThrow()
    {
        SocketRegistryService registry = CreateRegistry();

        await registry.SendToProfileAsync(ProfileA, CreateEvent());
    }

    [Fact]
    public async Task SendToUserAsync_WhenSendFails_RemovesSocketFromUser()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetUser(testSocket.Socket, UserA);
        testSocket.WebSocket.ThrowOnNextSend();

        await registry.SendToUserAsync(UserA, CreateEvent());
        await registry.SendToUserAsync(UserA, CreateEvent());

        Assert.Equal(0, testSocket.WebSocket.SendAttempts);
    }

    [Fact]
    public async Task SendToProfileAsync_WhenSendFails_RemovesSocketFromProfile()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetProfile(testSocket.Socket, ProfileA);
        testSocket.WebSocket.ThrowOnNextSend();

        await registry.SendToProfileAsync(ProfileA, CreateEvent());
        await registry.SendToProfileAsync(ProfileA, CreateEvent());

        Assert.Equal(0, testSocket.WebSocket.SendAttempts);
    }

    [Fact]
    public async Task ClosingSocket_RemovesItFromUserAndProfileBuckets()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetUser(testSocket.Socket, UserA);
        registry.SetProfile(testSocket.Socket, ProfileA);

        await testSocket.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");
        await registry.SendToUserAsync(UserA, CreateEvent());
        await registry.SendToProfileAsync(ProfileA, CreateEvent());

        AssertNoEventSent(testSocket);
    }

    private static SocketRegistryService CreateRegistry()
    {
        return new SocketRegistryService(NullLogger<SocketRegistryService>.Instance);
    }

    private static TestSocket CreateRegisteredSocket(SocketRegistryService registry)
    {
        FakeWebSocket webSocket = new();
        Socket socket = new Socket(webSocket);
        registry.RegisterSocket(socket);
        return new TestSocket(socket, webSocket);
    }

    private static ProfilesChangedEvent CreateEvent()
    {
        return new ProfilesChangedEvent()
        {
            Profiles = [new ProfileDto() { ProfileId = Guid.NewGuid(), Name = "x" }],
        };
    }

    private static void AssertSingleEventSent(TestSocket testSocket)
    {
        Assert.NotNull(testSocket.WebSocket.FirstSentText);
        Assert.Contains("ProfilesChangedEvent", testSocket.WebSocket.FirstSentText);
    }

    private static void AssertNoEventSent(TestSocket testSocket)
    {
        Assert.Equal(0, testSocket.WebSocket.SendAttempts);
    }

    private sealed record TestSocket(Socket Socket, FakeWebSocket WebSocket);
}
