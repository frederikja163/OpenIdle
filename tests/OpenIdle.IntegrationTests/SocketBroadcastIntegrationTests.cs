using System;
using System.Linq;
using Backend.Dtos;

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
    public async Task LoginAsTestUser_ReturnsResponseWithSameRequestId(CancellationToken ct)
    {
        using TestSocketClient socket = await _app.ConnectAsync(ct).ConfigureAwait(false);

        await socket.SendAsync(new LoginAsTestUserRequest { RequestId = "7" }, ct).ConfigureAwait(false);

        LoginAsTestUserResponse response = await ReceiveUntilAsync<LoginAsTestUserResponse>(socket, ct);
        Assert.That(response.RequestId, Is.EqualTo("7"));
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task UnknownRequestType_ReturnsErrorResponse(CancellationToken ct)
    {
        using TestSocketClient socket = await _app.ConnectAsync(ct).ConfigureAwait(false);

        await socket.SendRawAsync("{\"$type\":\"NotARealRequest\",\"id\":9}", ct).ConfigureAwait(false);

        ErrorResponse response = await ReceiveUntilAsync<ErrorResponse>(socket, ct);
        Assert.That(response.Message, Is.Not.Empty);
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task ProfilesChangedEvent_IsDeliveredToAllSocketsOfUser(CancellationToken ct)
    {
        using TestSocketClient a = await _app.ConnectAsync(ct).ConfigureAwait(false);
        using TestSocketClient b = await _app.ConnectAsync(ct).ConfigureAwait(false);
        using TestSocketClient d = await _app.ConnectAsync(ct).ConfigureAwait(false);
        using TestSocketClient e = await _app.ConnectAsync(ct).ConfigureAwait(false);

        string nameA = $"EvA{Guid.NewGuid():N}"[..8];
        string nameB = $"EvB{Guid.NewGuid():N}"[..8];
        string nameC = $"EvC{Guid.NewGuid():N}"[..8];

        await LoginAsync(a, ct);
        ProfilesChangedEvent evtA1 = await CreateProfileAsync(a, nameA, ct);
        AssertProfiles(evtA1, nameA);

        ProfilesChangedEvent evtA2 = await CreateProfileAsync(a, nameB, ct);
        AssertProfiles(evtA2, nameA, nameB);

        ListProfilesResponse aList = await ListProfilesAsync(a, ct);
        Guid profileAId = FindProfile(aList, nameA).ProfileId;
        Guid profileBId = FindProfile(aList, nameB).ProfileId;

        await LoginAsync(b, ct);
        Guid profileAIdB = FindProfile(await ListProfilesAsync(b, ct), nameA).ProfileId;
        await SelectProfileAsync(b, profileAIdB, ct);

        await LoginAsync(d, ct);
        Guid profileBIdD = FindProfile(await ListProfilesAsync(d, ct), nameB).ProfileId;
        await SelectProfileAsync(d, profileBIdD, ct);

        await LoginAsync(e, ct);

        ProfilesChangedEvent evtA3 = await CreateProfileAsync(a, nameC, ct);
        AssertProfiles(evtA3, nameA, nameB, nameC);
        AssertProfiles(await ReceiveProfilesChangedEventAsync(b, ct), nameA, nameB, nameC);
        AssertProfiles(await ReceiveProfilesChangedEventAsync(d, ct), nameA, nameB, nameC);
        AssertProfiles(await ReceiveProfilesChangedEventAsync(e, ct), nameA, nameB, nameC);
    }

    private static async Task LoginAsync(TestSocketClient socket, CancellationToken ct)
    {
        await socket.SendAsync(new LoginAsTestUserRequest { RequestId = "1" }, ct).ConfigureAwait(false);
        await ReceiveUntilAsync<LoginAsTestUserResponse>(socket, ct);
    }

    private static async Task<ProfilesChangedEvent> CreateProfileAsync(TestSocketClient socket, string name, CancellationToken ct)
    {
        await socket.SendAsync(new CreateProfileRequest { RequestId = "2", Name = name }, ct).ConfigureAwait(false);
        ProfilesChangedEvent evt = await ReceiveUntilAsync<ProfilesChangedEvent>(socket, ct);
        await ReceiveUntilAsync<CreateProfileResponse>(socket, ct);
        return evt;
    }

    private static async Task<ListProfilesResponse> ListProfilesAsync(TestSocketClient socket, CancellationToken ct)
    {
        await socket.SendAsync(new ListProfilesRequest { RequestId = "3" }, ct).ConfigureAwait(false);
        return await ReceiveUntilAsync<ListProfilesResponse>(socket, ct);
    }

    private static async Task<SelectProfileResponse> SelectProfileAsync(TestSocketClient socket, Guid profileId, CancellationToken ct)
    {
        await socket.SendAsync(new SelectProfileRequest { ProfileId = profileId, RequestId = "4" }, ct).ConfigureAwait(false);
        return await ReceiveUntilAsync<SelectProfileResponse>(socket, ct);
    }

    private static async Task<ProfilesChangedEvent> ReceiveProfilesChangedEventAsync(TestSocketClient socket, CancellationToken ct)
    {
        return await ReceiveUntilAsync<ProfilesChangedEvent>(socket, ct);
    }

    private static async Task<T> ReceiveUntilAsync<T>(TestSocketClient socket, CancellationToken ct) where T : DtoBase
    {
        DtoBase message = await socket.ReceiveUntilAsync(candidate => candidate is T, ct).ConfigureAwait(false);
        Assert.That(message, Is.InstanceOf<T>(), $"Expected {typeof(T).Name}, got {message.GetType().Name}");
        return (T)message;
    }

    private static void AssertProfiles(ProfilesChangedEvent evt, params string[] expectedNames)
    {
        Assert.That(evt.Profiles.Select(profile => profile.Name), Is.EquivalentTo(expectedNames));
    }

    private static ProfileDto FindProfile(ListProfilesResponse response, string name)
    {
        return response.Profiles.Single(profile => profile.Name == name);
    }
}
