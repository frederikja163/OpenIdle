using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Backend.Attributes;
using Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
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
            app.UseWebSockets(BuildWebSocketOptions(app));
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

    /// <summary>
    /// Restricts which sites may open a socket, from the AllowedWsOrigins
    /// configuration array (env: AllowedWsOrigins__0, AllowedWsOrigins__1, ...).
    /// </summary>
    /// <remarks>
    /// A WebSocket handshake is not subject to the browser's same-origin policy,
    /// so without this any page anywhere could drive this backend on a visitor's
    /// behalf. This is the check, not CORS.
    ///
    /// An empty list means "allow every origin", which is what ASP.NET Core does
    /// with no options at all. That is deliberate for local development, where the
    /// frontend's port varies; every deployed environment sets the list explicitly.
    ///
    /// Only requests that carry an Origin header are filtered — browsers always
    /// send one, other clients need not — so this hardens the browser attack path
    /// rather than authenticating callers.
    /// </remarks>
    private static WebSocketOptions BuildWebSocketOptions(WebApplication app)
    {
        WebSocketOptions options = new();
        string[] allowedOrigins = app.Configuration.GetSection("AllowedWsOrigins").Get<string[]>() ?? [];

        foreach (string origin in allowedOrigins)
        {
            options.AllowedOrigins.Add(origin);
        }

        Log.Info(allowedOrigins.Length == 0
            ? "WebSocket origins: unrestricted (AllowedWsOrigins is empty)"
            : $"WebSocket origins restricted to: {string.Join(", ", allowedOrigins)}");

        return options;
    }
}
