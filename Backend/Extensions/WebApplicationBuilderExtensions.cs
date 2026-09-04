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
        /// CORS for the plumbing HTTP endpoints (currently /version), served from
        /// the same origin allowlist as the WebSocket handshake: one list governs
        /// both. Like the WS handshake, an empty list means unrestricted, which is
        /// what local development wants.
        /// </summary>
        internal void AddOpenIdleCors(IConfiguration configuration)
        {
            string[] allowedOrigins = ReadAllowedOrigins(configuration);
            collection.AddCors(options => options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    policy.AllowAnyOrigin();
                }
                else
                {
                    policy.WithOrigins(allowedOrigins);
                }
            }));
            Log.Info(allowedOrigins.Length == 0
                ? "CORS origins: unrestricted (AllowedWsOrigins is empty)"
                : $"CORS origins restricted to: {string.Join(", ", allowedOrigins)}");
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
