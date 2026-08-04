using System;
using System.Text.Json;

namespace OpenIdle.IntegrationTests;

public sealed class SocketBroadcastIntegrationTests : IDisposable
{
    private readonly TestApplication _app;

    public SocketBroadcastIntegrationTests()
    {
        _app = new TestApplication();
    }

    public void Dispose()
    {
        _app.Dispose();
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task Ping_ReturnsPongWithSameId(CancellationToken ct)
    {
        using TestSocket socket = await _app.ConnectAsync(ct).ConfigureAwait(false);

        await socket.SendAsync("{\"$type\":\"PingRequest\",\"id\":7}", ct).ConfigureAwait(false);

        string message = await socket.ReceiveAsync(ct).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("\"PongResponse\""));
            Assert.That(message, Does.Contain("\"id\":7"));
        });
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task UnknownRequestType_ReturnsErrorResponse(CancellationToken ct)
    {
        using TestSocket socket = await _app.ConnectAsync(ct).ConfigureAwait(false);

        await socket.SendAsync("{\"$type\":\"NotARealRequest\",\"id\":9}", ct).ConfigureAwait(false);

        string message = await socket.ReceiveAsync(ct).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("\"ErrorResponse\""));
            Assert.That(message, Does.Contain("\"message\""));
        });
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task ProfilesChangedEvent_IsDeliveredToAllSocketsOfUser(CancellationToken ct)
    {
        using TestSocket a = await _app.ConnectAsync(ct).ConfigureAwait(false);
        using TestSocket b = await _app.ConnectAsync(ct).ConfigureAwait(false);
        using TestSocket d = await _app.ConnectAsync(ct).ConfigureAwait(false);
        using TestSocket e = await _app.ConnectAsync(ct).ConfigureAwait(false);

        string nameA = $"EvA{Guid.NewGuid():N}"[..8];
        string nameB = $"EvB{Guid.NewGuid():N}"[..8];
        string nameC = $"EvC{Guid.NewGuid():N}"[..8];

        await a.SendAsync("{\"$type\":\"LoginAsTestUserRequest\",\"id\":1}", ct).ConfigureAwait(false);
        Assert.That(await a.ReceiveAsync(ct).ConfigureAwait(false), Does.Contain("\"LoginAsTestUserResponse\""));

        await a.SendAsync($"{{\"$type\":\"CreateProfileRequest\",\"id\":2,\"name\":\"{nameA}\"}}", ct).ConfigureAwait(false);
        AssertProfileEvent(await a.ReceiveAsync(ct).ConfigureAwait(false), nameA);
        AssertResponse(await a.ReceiveAsync(ct).ConfigureAwait(false), "CreateProfileResponse", 2);

        await a.SendAsync($"{{\"$type\":\"CreateProfileRequest\",\"id\":3,\"name\":\"{nameB}\"}}", ct).ConfigureAwait(false);
        AssertProfileEvent(await a.ReceiveAsync(ct).ConfigureAwait(false), nameA, nameB);
        AssertResponse(await a.ReceiveAsync(ct).ConfigureAwait(false), "CreateProfileResponse", 3);

        await a.SendAsync("{\"$type\":\"ListProfilesRequest\",\"id\":4}", ct).ConfigureAwait(false);
        string aList = await a.ReceiveAsync(ct).ConfigureAwait(false);
        Assert.That(aList, Does.Contain("\"ListProfilesResponse\""));
        Guid profileAId = ParseProfileId(aList, nameA);
        Guid profileBId = ParseProfileId(aList, nameB);

        await b.SendAsync("{\"$type\":\"LoginAsTestUserRequest\",\"id\":1}", ct).ConfigureAwait(false);
        Assert.That(await b.ReceiveAsync(ct).ConfigureAwait(false), Does.Contain("\"LoginAsTestUserResponse\""));
        await b.SendAsync("{\"$type\":\"ListProfilesRequest\",\"id\":2}", ct).ConfigureAwait(false);
        Guid profileAIdB = ParseProfileId(await b.ReceiveAsync(ct).ConfigureAwait(false), nameA);
        await b.SendAsync($"{{\"$type\":\"SelectProfileRequest\",\"id\":3,\"profileId\":\"{profileAIdB}\"}}", ct).ConfigureAwait(false);
        AssertResponse(await b.ReceiveAsync(ct).ConfigureAwait(false), "SelectProfileResponse", 3);

        await d.SendAsync("{\"$type\":\"LoginAsTestUserRequest\",\"id\":1}", ct).ConfigureAwait(false);
        Assert.That(await d.ReceiveAsync(ct).ConfigureAwait(false), Does.Contain("\"LoginAsTestUserResponse\""));
        await d.SendAsync("{\"$type\":\"ListProfilesRequest\",\"id\":2}", ct).ConfigureAwait(false);
        Guid profileBIdD = ParseProfileId(await d.ReceiveAsync(ct).ConfigureAwait(false), nameB);
        await d.SendAsync($"{{\"$type\":\"SelectProfileRequest\",\"id\":3,\"profileId\":\"{profileBIdD}\"}}", ct).ConfigureAwait(false);
        AssertResponse(await d.ReceiveAsync(ct).ConfigureAwait(false), "SelectProfileResponse", 3);

        await e.SendAsync("{\"$type\":\"LoginAsTestUserRequest\",\"id\":1}", ct).ConfigureAwait(false);
        Assert.That(await e.ReceiveAsync(ct).ConfigureAwait(false), Does.Contain("\"LoginAsTestUserResponse\""));

        await a.SendAsync($"{{\"$type\":\"CreateProfileRequest\",\"id\":5,\"name\":\"{nameC}\"}}", ct).ConfigureAwait(false);
        AssertProfileEvent(await a.ReceiveAsync(ct).ConfigureAwait(false), nameA, nameB, nameC);
        AssertResponse(await a.ReceiveAsync(ct).ConfigureAwait(false), "CreateProfileResponse", 5);
        AssertProfileEvent(await b.ReceiveAsync(ct).ConfigureAwait(false), nameA, nameB, nameC);
        AssertProfileEvent(await d.ReceiveAsync(ct).ConfigureAwait(false), nameA, nameB, nameC);
        AssertProfileEvent(await e.ReceiveAsync(ct).ConfigureAwait(false), nameA, nameB, nameC);
    }

    private static void AssertResponse(string json, string expectedType, int expectedId)
    {
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain($"\"{expectedType}\""), $"Expected {expectedType}, got: {json}");
            Assert.That(json, Does.Contain($"\"id\":{expectedId}"), $"Expected id {expectedId}, got: {json}");
        });
    }

    private static void AssertProfileEvent(string json, params string[] expectedNames)
    {
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"ProfilesChangedEvent\""), $"Expected ProfilesChangedEvent, got: {json}");
            foreach (string name in expectedNames)
            {
                Assert.That(json, Does.Contain($"\"name\":\"{name}\""),
                    $"ProfilesChangedEvent is missing profile '{name}': {json}");
            }
        });
    }

    private static Guid ParseProfileId(string listJson, string name)
    {
        using JsonDocument document = JsonDocument.Parse(listJson);
        foreach (JsonElement profile in document.RootElement.GetProperty("profiles").EnumerateArray())
        {
            if (profile.GetProperty("name").GetString() == name)
            {
                return profile.GetProperty("profileId").GetGuid();
            }
        }

        Assert.Fail($"Profile '{name}' not found in list response: {listJson}");
        return Guid.Empty;
    }
}
