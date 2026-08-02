using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Backend.Dtos;
using Backend.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Services;

public sealed class SocketEndpointService : IHostedService
{
    private record RegisteredEndpoint(Type ControllerType, MethodInfo MethodInfo, Type DtoType);

    private readonly IServiceProvider _provider;
    private readonly SocketRegistryService _socketRegistry;
    private readonly ILogger<SocketEndpointService> _logger;
    private readonly Dictionary<Type, List<RegisteredEndpoint>> _endpoints = new();

    public SocketEndpointService(IServiceProvider provider, SocketRegistryService socketRegistry,
        ILogger<SocketEndpointService> logger)
    {
        _provider = provider;
        _socketRegistry = socketRegistry;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _socketRegistry.MessageReceived += SocketRegistryOnMessageReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _socketRegistry.MessageReceived -= SocketRegistryOnMessageReceived;
        return Task.CompletedTask;
    }

    private async Task SocketRegistryOnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        RequestBase request = e.Request;
        Socket socket = ArgumentException.ThrowIfNotOfType<Socket>(sender);
        if (!_endpoints.TryGetValue(request.GetType(), out List<RegisteredEndpoint>? registeredEndpoints))
        {
            // TODO: Send invalid request error to socket.
            return;
        }

        try
        {
            List<Task> tasks = [];
            foreach (RegisteredEndpoint registeredEndpoint in registeredEndpoints)
            {
                object controller = ActivatorUtilities.CreateInstance(_provider, registeredEndpoint.ControllerType);
                if (controller is SocketControllerBase socketControllerBase)
                {
                    socketControllerBase.Context = new SocketControllerContext(socket, request);
                }

                object? result = registeredEndpoint.MethodInfo.Invoke(controller, [request]);
                if (result is Task task)
                {
                    tasks.Add(task);
                }
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to handle request of type {DtoType}.", request.GetType().Name);
        }
    }

    internal void TryRegisterEndpoint(MethodInfo methodInfo)
    {
        ArgumentNullException.ThrowIfNull(methodInfo.DeclaringType);
        Type controllerType = methodInfo.DeclaringType;
        
        ParameterInfo[] parameterInfos = methodInfo.GetParameters();
        if (parameterInfos.Length != 1)
        {
            throw new ArgumentException(
                $"Method {controllerType.Name}.{methodInfo.Name} must declare exactly one parameter derived from {nameof(RequestBase)}.",
                nameof(methodInfo));
        }

        ParameterInfo parameter = parameterInfos.First();
        if (!parameter.ParameterType.IsAssignableTo(typeof(RequestBase)))
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Name}' of method {controllerType.Name}.{methodInfo.Name} must be derived from {nameof(RequestBase)}.",
                nameof(methodInfo));
        }

        Type dtoType = parameter.ParameterType;
        GetOrCreateEndpoints(dtoType).Add(new RegisteredEndpoint(controllerType, methodInfo, dtoType));
    }

    private List<RegisteredEndpoint> GetOrCreateEndpoints(Type dtoType)
    {
        if (!_endpoints.TryGetValue(dtoType, out List<RegisteredEndpoint>? registeredEndpoints))
        {
            registeredEndpoints = [];
            _endpoints[dtoType] = registeredEndpoints;
        }

        return registeredEndpoints;
    }
}