using System.Net.WebSockets;
using System.Text.Json;
using Backend;
using Backend.Dtos;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
        registry.SetProfile(first.Socket, ProfileA);
        registry.SetProfile(second.Socket, ProfileB);

        await registry.SendToProfileAsync(ProfileA, CreateEvent());

        AssertSingleEventSent(first);
        AssertNoEventSent(second);
    }

    [Test]
    public async Task SendToProfileAsync_MovedSocket_LeavesPreviousProfileBucket()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetProfile(testSocket.Socket, ProfileA);

        registry.SetProfile(testSocket.Socket, ProfileB);

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
        registry.SetProfile(testSocket.Socket, ProfileA);
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
        registry.SetProfile(testSocket.Socket, ProfileA);
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
        registry.SetProfile(testSocket.Socket, ProfileA);

        await testSocket.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");
        await registry.SendToUserAsync(UserA, CreateEvent());
        await registry.SendToProfileAsync(ProfileA, CreateEvent());

        AssertNoEventSent(testSocket);
    }

    [Test]
    public async Task SendToUserAsync_AssignsIncrementingEventIdsAndTimestampPerSocket()
    {
        SocketRegistryService registry = CreateRegistry();
        TestSocket testSocket = CreateRegisteredSocket(registry);
        registry.SetUser(testSocket.Socket, UserA);

        long beforeSend = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await registry.SendToUserAsync(UserA, CreateEvent());
        await registry.SendToUserAsync(UserA, CreateEvent());
        long afterSend = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.That(testSocket.WebSocket.SendAttempts, Is.EqualTo(2));
        using JsonDocument first = JsonDocument.Parse(testSocket.WebSocket.Sent[0].Bytes);
        using JsonDocument second = JsonDocument.Parse(testSocket.WebSocket.Sent[1].Bytes);
        JsonElement firstRoot = first.RootElement;
        JsonElement timestamp = firstRoot.GetProperty("timestamp");
        Assert.Multiple(() =>
        {
            Assert.That(firstRoot.GetProperty("eventId").GetInt32(), Is.EqualTo(0));
            Assert.That(timestamp.ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(timestamp.TryGetInt64(out long timestampValue), Is.True);
            Assert.That(timestampValue, Is.InRange(beforeSend, afterSend));
            Assert.That(second.RootElement.GetProperty("eventId").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SendEventAsync_ConcurrentCalls_SendSequentialUniqueIdsInOrder()
    {
        const int eventCount = 200;
        FakeWebSocket webSocket = new();
        Socket socket = new(webSocket);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task[] sends = Enumerable.Range(0, eventCount)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;
                await socket.SendEventAsync(CreateEvent());
            }))
            .ToArray();

        start.SetResult();
        await Task.WhenAll(sends);

        int[] sentIds = webSocket.Sent
            .Select(frame => JsonDocument.Parse(frame.Bytes))
            .Select(document =>
            {
                using (document)
                {
                    return document.RootElement.GetProperty("eventId").GetInt32();
                }
            })
            .ToArray();
        Assert.That(sentIds, Is.EqualTo(Enumerable.Range(0, eventCount)));
        Assert.That(sentIds.Distinct().Count(), Is.EqualTo(eventCount));
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
