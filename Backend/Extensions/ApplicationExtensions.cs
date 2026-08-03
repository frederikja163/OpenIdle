using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Backend.Attributes;
using Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Extensions;

internal static class WebApplicationBuilderExtensions
{
    extension(IServiceCollection collection)
    {
        internal void AddSocketControllers()
        {
            collection.AddSingleton<SocketRegistryService>();
            collection.AddSingleton<SocketEndpointService>();
            collection.AddHostedService(sp => sp.GetRequiredService<SocketEndpointService>());
        }
    }

    extension(WebApplication app)
    {
        internal void MapSocketControllers()
        {
            app.UseWebSockets();
            SocketEndpointService socketEndpointService = app.Services.GetRequiredService<SocketEndpointService>();

            IEnumerable<MethodInfo> methodInfos = Assembly.GetExecutingAssembly()
                .GetExportedTypes()
                .Where(t => t.GetCustomAttribute(typeof(SocketControllerAttribute)) is { })
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(m => m.GetCustomAttribute(typeof(RequestAttribute)) is { });
            
            foreach (MethodInfo methodInfo in methodInfos)
            {
                socketEndpointService.TryRegisterEndpoint(methodInfo);
            }
        }
    }
}