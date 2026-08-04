using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backend;
using Backend.Attributes;
using Backend.Dtos;
using Backend.Dtos.Auth;
using Backend.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdle.Tests.TestDoubles;

namespace OpenIdle.Tests.Services;

public sealed class SocketEndpointServiceTests
{
    [Fact]
    public void TryRegisterEndpoint_WithNoParameters_Throws()
    {
        SocketEndpointService service = CreateService(out _);

        Assert.Throws<ArgumentException>(() =>
            service.TryRegisterEndpoint(GetMethod<ValidationProbeController>(nameof(ValidationProbeController.NoParams))));
    }

    [Fact]
    public void TryRegisterEndpoint_WithMultipleParameters_Throws()
    {
        SocketEndpointService service = CreateService(out _);

        Assert.Throws<ArgumentException>(() =>
            service.TryRegisterEndpoint(GetMethod<ValidationProbeController>(nameof(ValidationProbeController.TwoParams))));
    }

    [Fact]
    public void TryRegisterEndpoint_WithNonRequestParameter_Throws()
    {
        SocketEndpointService service = CreateService(out _);

        Assert.Throws<ArgumentException>(() =>
            service.TryRegisterEndpoint(GetMethod<ValidationProbeController>(nameof(ValidationProbeController.WrongParam))));
    }

    [Fact]
    public void TryRegisterEndpoint_WithValidHandler_DoesNotThrow()
    {
        SocketEndpointService service = CreateService(out _);

        service.TryRegisterEndpoint(GetMethod<TestPingController>(nameof(TestPingController.Ping)));
    }

    [Fact]
    public async Task Dispatch_InvokesHandlerAndSendsResponse()
    {
        SocketEndpointService service = CreateService(out SocketRegistryService registry);
        await service.StartAsync(CancellationToken.None);
        service.TryRegisterEndpoint(GetMethod<TestPingController>(nameof(TestPingController.Ping)));
        FakeWebSocket webSocket = RegisterSocket(registry, out Socket socket);

        webSocket.EnqueueReceive(Serialize(new PingRequest() { Id = 42 }));
        webSocket.EnqueueClose();

        await socket.StartAsync(CancellationToken.None);

        Assert.NotNull(webSocket.FirstSentText);
        Assert.Contains("PongResponse", webSocket.FirstSentText);
        Assert.Contains("\"Id\":42", webSocket.FirstSentText);
    }

    [Fact]
    public async Task Dispatch_UnknownRequestType_RespondsWithError()
    {
        SocketEndpointService service = CreateService(out SocketRegistryService registry);
        await service.StartAsync(CancellationToken.None);
        FakeWebSocket webSocket = RegisterSocket(registry, out Socket socket);

        webSocket.EnqueueReceive(Serialize(new ListProfilesRequest()));
        webSocket.EnqueueClose();

        await socket.StartAsync(CancellationToken.None);

        Assert.NotNull(webSocket.FirstSentText);
        Assert.Contains("ErrorResponse", webSocket.FirstSentText);
        Assert.Contains("No handler registered for this request type.", webSocket.FirstSentText);
    }

    [Fact]
    public async Task Dispatch_InvokesAllHandlersForRequestType()
    {
        SocketEndpointService service = CreateService(out SocketRegistryService registry);
        await service.StartAsync(CancellationToken.None);
        service.TryRegisterEndpoint(GetMethod<TestPingController>(nameof(TestPingController.Ping)));
        service.TryRegisterEndpoint(GetMethod<SecondTestPingController>(nameof(SecondTestPingController.Ping)));
        FakeWebSocket webSocket = RegisterSocket(registry, out Socket socket);

        webSocket.EnqueueReceive(Serialize(new PingRequest()));
        webSocket.EnqueueClose();

        await socket.StartAsync(CancellationToken.None);

        Assert.Equal(2, webSocket.SendAttempts);
    }

    [Fact]
    public async Task Dispatch_HandlerSettingUser_AllowsUserEvents()
    {
        SocketEndpointService service = CreateService(out SocketRegistryService registry);
        await service.StartAsync(CancellationToken.None);
        service.TryRegisterEndpoint(GetMethod<TestUserController>(nameof(TestUserController.Notify)));
        FakeWebSocket webSocket = RegisterSocket(registry, out Socket socket);
        Backend.Entities.User user = new() { UserId = Guid.NewGuid() };
        socket.User = user;
        registry.SetUser(socket, user);

        webSocket.EnqueueReceive(Serialize(new PingRequest()));
        webSocket.EnqueueClose();

        await socket.StartAsync(CancellationToken.None);

        Assert.NotNull(webSocket.FirstSentText);
        Assert.Contains("ProfilesChangedEvent", webSocket.FirstSentText);
    }

    private static SocketEndpointService CreateService(out SocketRegistryService registry)
    {
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        registry = new SocketRegistryService(NullLogger<SocketRegistryService>.Instance);
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
        return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, typeof(DtoBase)));
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
    public async Task Ping(PingRequest request)
    {
        await RespondAsync(new PongResponse());
    }
}

[SocketController]
public sealed class SecondTestPingController : SocketControllerBase
{
    [Request]
    public async Task Ping(PingRequest request)
    {
        await RespondAsync(new PongResponse());
    }
}

[SocketController]
public sealed class TestUserController : SocketControllerBase
{
    [Request]
    public async Task Notify(PingRequest request)
    {
        await SendUserEventAsync(new ProfilesChangedEvent()
        {
            Profiles = [new ProfileDto() { ProfileId = Guid.NewGuid(), Name = "x" }],
        });
    }
}

public sealed class ValidationProbeController : SocketControllerBase
{
    public Task NoParams() => Task.CompletedTask;

    public Task TwoParams(PingRequest first, PingRequest second) => Task.CompletedTask;

    public Task WrongParam(string text) => Task.CompletedTask;
}
