using System.Reflection;
using Backend;
using Backend.Attributes;
using Backend.Dtos;
using Backend.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdle.Tests.TestDoubles;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class SocketEndpointServiceTests
{
    [Test]
    public void TryRegisterEndpoint_WithNoParameters_Throws()
    {
        SocketEndpointService service = CreateService(out _);

        Assert.That(() =>
            service.TryRegisterEndpoint(GetMethod<ValidationProbeController>(nameof(ValidationProbeController.NoParams))),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void TryRegisterEndpoint_WithMultipleParameters_Throws()
    {
        SocketEndpointService service = CreateService(out _);

        Assert.That(() =>
            service.TryRegisterEndpoint(GetMethod<ValidationProbeController>(nameof(ValidationProbeController.TwoParams))),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void TryRegisterEndpoint_WithNonRequestParameter_Throws()
    {
        SocketEndpointService service = CreateService(out _);

        Assert.That(() =>
            service.TryRegisterEndpoint(GetMethod<ValidationProbeController>(nameof(ValidationProbeController.WrongParam))),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void TryRegisterEndpoint_WithValidHandler_DoesNotThrow()
    {
        SocketEndpointService service = CreateService(out _);

        service.TryRegisterEndpoint(GetMethod<TestPingController>(nameof(TestPingController.Ping)));
    }

    [Test]
    public async Task Dispatch_InvokesHandlerAndSendsResponse()
    {
        SocketEndpointService service = CreateService(out SocketRegistryService registry);
        await service.StartAsync(CancellationToken.None);
        service.TryRegisterEndpoint(GetMethod<TestPingController>(nameof(TestPingController.Ping)));
        FakeWebSocket webSocket = RegisterSocket(registry, out Socket socket);

        webSocket.EnqueueReceive(Serialize(new CreateProfileRequest() { Name = "x", RequestId = 42 }));
        webSocket.EnqueueClose();

        await socket.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(webSocket.FirstSentText, Is.Not.Null);
            Assert.That(webSocket.FirstSentText, Does.Contain("CreateProfileResponse"));
            Assert.That(webSocket.FirstSentText, Does.Contain("\"requestId\":42"));
        });
    }

    [Test]
    public async Task Dispatch_UnknownRequestType_RespondsWithError()
    {
        SocketEndpointService service = CreateService(out SocketRegistryService registry);
        await service.StartAsync(CancellationToken.None);
        FakeWebSocket webSocket = RegisterSocket(registry, out Socket socket);

        webSocket.EnqueueReceive(Serialize(new ListProfilesRequest()));
        webSocket.EnqueueClose();

        await socket.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(webSocket.FirstSentText, Is.Not.Null);
            Assert.That(webSocket.FirstSentText, Does.Contain("ErrorResponse"));
            Assert.That(webSocket.FirstSentText, Does.Contain("No handler registered for this request type."));
        });
    }

    [Test]
    public async Task Dispatch_InvokesAllHandlersForRequestType()
    {
        SocketEndpointService service = CreateService(out SocketRegistryService registry);
        await service.StartAsync(CancellationToken.None);
        service.TryRegisterEndpoint(GetMethod<TestPingController>(nameof(TestPingController.Ping)));
        service.TryRegisterEndpoint(GetMethod<SecondTestPingController>(nameof(SecondTestPingController.Ping)));
        FakeWebSocket webSocket = RegisterSocket(registry, out Socket socket);

        webSocket.EnqueueReceive(Serialize(new CreateProfileRequest() { Name = "x" }));
        webSocket.EnqueueClose();

        await socket.StartAsync(CancellationToken.None);

        Assert.That(webSocket.SendAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Dispatch_HandlerSettingUser_AllowsUserEvents()
    {
        SocketEndpointService service = CreateService(out SocketRegistryService registry);
        await service.StartAsync(CancellationToken.None);
        service.TryRegisterEndpoint(GetMethod<TestUserController>(nameof(TestUserController.Notify)));
        FakeWebSocket webSocket = RegisterSocket(registry, out Socket socket);
        Guid userId = Guid.NewGuid();
        socket.UserId = userId;
        registry.SetUser(socket, userId);

        webSocket.EnqueueReceive(Serialize(new CreateProfileRequest() { Name = "x" }));
        webSocket.EnqueueClose();

        await socket.StartAsync(CancellationToken.None);

        Assert.That(webSocket.FirstSentText, Is.Not.Null);
        Assert.That(webSocket.FirstSentText, Does.Contain("ProfilesChangedEvent"));
    }

    private static SocketEndpointService CreateService(out SocketRegistryService registry)
    {
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        registry = new SocketRegistryService();
        return new SocketEndpointService(provider, registry, NullLogger<SocketEndpointService>.Instance);
    }

    private static FakeWebSocket RegisterSocket(SocketRegistryService registry, out Socket socket)
    {
        FakeWebSocket webSocket = new();
        socket = new Socket(webSocket);
        registry.RegisterSocket(socket);
        return webSocket;
    }

    private static byte[] Serialize(RequestBase request)
    {
        return SocketJsonSerializer.Serialize(request);
    }

    private static MethodInfo GetMethod<T>(string name)
    {
        return typeof(T).GetMethod(name)!;
    }
}

[SocketController]
public sealed class TestPingController : SocketControllerBase
{
    [Request]
    public async Task Ping(CreateProfileRequest request)
    {
        await RespondAsync(new CreateProfileResponse());
    }
}

[SocketController]
public sealed class SecondTestPingController : SocketControllerBase
{
    [Request]
    public async Task Ping(CreateProfileRequest request)
    {
        await RespondAsync(new CreateProfileResponse());
    }
}

[SocketController]
public sealed class TestUserController : SocketControllerBase
{
    [Request]
    public async Task Notify(CreateProfileRequest request)
    {
        await SendUserEventAsync(new ProfilesChangedEvent()
        {
            Profiles =
            [
                new ProfileDto()
                {
                    ProfileId = Guid.NewGuid(),
                    Name = "x",
                    TotalLevel = 0,
                    CreationTime = 0,
                    LastActive = null
                }
            ],
        });
    }
}

public sealed class ValidationProbeController : SocketControllerBase
{
    public Task NoParams() => Task.CompletedTask;

    public Task TwoParams(CreateProfileRequest first, CreateProfileRequest second) => Task.CompletedTask;

    public Task WrongParam(string text) => Task.CompletedTask;
}
