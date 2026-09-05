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

        /// <summary>
        /// The HTTP side of the backend is public: it serves read-only plumbing
        /// (/health, /version) that any origin may read, and it is meant to be
        /// reachable as a public API. So every HTTP endpoint answers any origin.
        /// Only the WebSocket handshake is origin-gated, via AllowedWsOrigins in
        /// BuildWebSocketOptions, because the socket is where the session lives.
        /// Browsers still need the Access-Control-Allow-Origin header to let a
        /// page read a cross-origin response, which is why this cannot simply be
        /// left out.
        /// </summary>
        internal void AddOpenIdleCors()
        {
            collection.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin()));
        }
    }

    extension(WebApplication app)
    {
        internal void UseOpenIdleCors() => app.UseCors();

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

    private static string[] ReadAllowedOrigins(IConfiguration configuration)
    {
        return configuration.GetSection("AllowedWsOrigins").Get<string[]>() ?? [];
    }

    private static WebSocketOptions BuildWebSocketOptions(WebApplication app)
    {
        WebSocketOptions options = new();
        string[] allowedOrigins = ReadAllowedOrigins(app.Configuration);

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
