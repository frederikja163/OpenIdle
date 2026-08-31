using System.Net.WebSockets;
using Backend;
using Backend.Dtos;
using Backend.Services;
using OpenIdle.Tests.TestDoubles;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class SocketRegistryServiceTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private static readonly Guid ProfileA = Guid.NewGuid();
    private static readonly Guid ProfileB = Guid.NewGuid();

    [Test]
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

    [Test]
    public async Task SendToUserAsync_MovedSocket_LeavesPreviousUserBucket()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetUser(testSocket.Socket, UserA);

        registry.SetUser(testSocket.Socket, UserB);

        await registry.SendToUserAsync(UserA, CreateEvent());
        await registry.SendToUserAsync(UserB, CreateEvent());
        Assert.That(testSocket.WebSocket.SendAttempts, Is.EqualTo(1));
        Assert.That(testSocket.WebSocket.FirstSentText, Does.Contain("ProfilesChangedEvent"));
    }

    [Test]
    public async Task SendToProfileAsync_DeliversEventToAllSocketsOfProfile()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket first = CreateRegisteredSocket(registry);
        TestSocket second = CreateRegisteredSocket(registry);
        registry.SetUser(first.Socket, UserA);
        registry.SetUser(second.Socket, UserA);
        await registry.SetProfile(first.Socket, ProfileA);
        await registry.SetProfile(second.Socket, ProfileB);

        await registry.SendToProfileAsync(ProfileA, CreateEvent());

        AssertSingleEventSent(first);
        AssertNoEventSent(second);
    }

    [Test]
    public async Task SendToProfileAsync_MovedSocket_LeavesPreviousProfileBucket()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        await registry.SetProfile(testSocket.Socket, ProfileA);

        await registry.SetProfile(testSocket.Socket, ProfileB);

        await registry.SendToProfileAsync(ProfileA, CreateEvent());
        await registry.SendToProfileAsync(ProfileB, CreateEvent());
        Assert.That(testSocket.WebSocket.SendAttempts, Is.EqualTo(1));
        Assert.That(testSocket.WebSocket.FirstSentText, Does.Contain("ProfilesChangedEvent"));
    }

    [Test]
    public async Task SendToUserAsync_WithNoSockets_DoesNotThrow()
    {
        SocketRegistryService registry = CreateRegistry();

        await registry.SendToUserAsync(UserA, CreateEvent());
    }

    [Test]
    public async Task SendToProfileAsync_WithNoSockets_DoesNotThrow()
    {
        SocketRegistryService registry = CreateRegistry();

        await registry.SendToProfileAsync(ProfileA, CreateEvent());
    }

    [Test]
    public async Task SendToUserAsync_WhenSendFails_RemovesSocketFromUser()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetUser(testSocket.Socket, UserA);
        testSocket.WebSocket.ThrowOnNextSend();

        await registry.SendToUserAsync(UserA, CreateEvent());
        await registry.SendToUserAsync(UserA, CreateEvent());

        Assert.That(testSocket.WebSocket.SendAttempts, Is.EqualTo(0));
    }

    [Test]
    public async Task SendToProfileAsync_WhenSendFails_RemovesSocketFromProfile()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        await registry.SetProfile(testSocket.Socket, ProfileA);
        testSocket.WebSocket.ThrowOnNextSend();

        await registry.SendToProfileAsync(ProfileA, CreateEvent());
        await registry.SendToProfileAsync(ProfileA, CreateEvent());

        Assert.That(testSocket.WebSocket.SendAttempts, Is.EqualTo(0));
    }

    [Test]
    public async Task SendToUserAsync_NonTransportException_DoesNotRemoveSocket()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetUser(testSocket.Socket, UserA);
        testSocket.WebSocket.ThrowNonTransportOnNextSend();

        Assert.ThrowsAsync<InvalidOperationException>(() => registry.SendToUserAsync(UserA, CreateEvent()));

        await registry.SendToUserAsync(UserA, CreateEvent());
        Assert.That(testSocket.WebSocket.SendAttempts, Is.EqualTo(1));
    }

    [Test]
    public async Task SendToProfileAsync_NonTransportException_DoesNotRemoveSocket()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        await registry.SetProfile(testSocket.Socket, ProfileA);
        testSocket.WebSocket.ThrowNonTransportOnNextSend();

        Assert.ThrowsAsync<InvalidOperationException>(() => registry.SendToProfileAsync(ProfileA, CreateEvent()));

        await registry.SendToProfileAsync(ProfileA, CreateEvent());
        Assert.That(testSocket.WebSocket.SendAttempts, Is.EqualTo(1));
    }

    [Test]
    public async Task ClosingSocket_RemovesItFromUserAndProfileBuckets()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetUser(testSocket.Socket, UserA);
        await registry.SetProfile(testSocket.Socket, ProfileA);

        await testSocket.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");
        await registry.SendToUserAsync(UserA, CreateEvent());
        await registry.SendToProfileAsync(ProfileA, CreateEvent());

        AssertNoEventSent(testSocket);
    }

    [Test]
    public async Task ProfileOnline_RaisedOnlyForFirstSocketOnProfile()
    {
        SocketRegistryService registry = CreateRegistry();
        List<Guid> raised = [];
        registry.ProfileOnline += (_, e) =>
        {
            raised.Add(e.ProfileId);
            return Task.CompletedTask;
        };

        TestSocket first = CreateRegisteredSocket(registry);
        TestSocket second = CreateRegisteredSocket(registry);
        TestSocket other = CreateRegisteredSocket(registry);

        await registry.SetProfile(first.Socket, ProfileA);
        await registry.SetProfile(second.Socket, ProfileA);
        await registry.SetProfile(other.Socket, ProfileB);

        Assert.That(raised, Is.EqualTo(new[] { ProfileA, ProfileB }));
    }

    [Test]
    public async Task ProfileOffline_RaisedOnlyWhenLastSocketLeavesProfile()
    {
        SocketRegistryService registry = CreateRegistry();
        List<Guid> raised = [];
        registry.ProfileOnline += (_, _) => Task.CompletedTask;
        registry.ProfileOffline += (_, e) =>
        {
            raised.Add(e.ProfileId);
            return Task.CompletedTask;
        };

        TestSocket first = CreateRegisteredSocket(registry);
        TestSocket second = CreateRegisteredSocket(registry);
        TestSocket other = CreateRegisteredSocket(registry);
        await registry.SetProfile(first.Socket, ProfileA);
        await registry.SetProfile(second.Socket, ProfileA);
        await registry.SetProfile(other.Socket, ProfileB);

        await first.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");
        await second.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");
        await other.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");

        Assert.That(raised, Is.EqualTo(new[] { ProfileA, ProfileB }));
    }

    [Test]
    public async Task ProfileOnline_RaisedAgainWhenProfileEmptiesAndRefills()
    {
        SocketRegistryService registry = CreateRegistry();
        List<Guid> raised = [];
        registry.ProfileOnline += (_, e) =>
        {
            raised.Add(e.ProfileId);
            return Task.CompletedTask;
        };

        TestSocket first = CreateRegisteredSocket(registry);
        TestSocket second = CreateRegisteredSocket(registry);
        await registry.SetProfile(first.Socket, ProfileA);
        await first.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");

        await registry.SetProfile(second.Socket, ProfileA);

        Assert.That(raised, Is.EqualTo(new[] { ProfileA, ProfileA }));
    }

    private static SocketRegistryService CreateRegistry()
    {
        return new SocketRegistryService();
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
        Assert.Multiple(() =>
        {
            Assert.That(testSocket.WebSocket.FirstSentText, Is.Not.Null);
            Assert.That(testSocket.WebSocket.FirstSentText, Does.Contain("ProfilesChangedEvent"));
        });
    }

    private static void AssertNoEventSent(TestSocket testSocket)
    {
        Assert.That(testSocket.WebSocket.SendAttempts, Is.EqualTo(0));
    }

    private sealed record TestSocket(Socket Socket, FakeWebSocket WebSocket);
}
