using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Dtos;
using Backend.Extensions;
using Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backend;

internal static class AppHost
{
    public static WebApplication CreateApp(string[] args, string? connectionString = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.AddDbContextFactory<GameDbContext>(options =>
            options.UseSqlite(connectionString ?? builder.Configuration.GetConnectionString("Default")));
        builder.Services.AddControllers().AddApplicationPart(typeof(Backend.Controllers.Http.WsController).Assembly);
        builder.Services.AddSocketControllers();
        // The HTTP API is public read-only plumbing (/health, /version), so every
        // endpoint answers any origin; browsers still need Access-Control-Allow-Origin
        // to read a cross-origin response. Only the WebSocket handshake is origin-gated
        // (AllowedWsOrigins in BuildWebSocketOptions), since the socket holds the session.
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin()));
        builder.Services.AddSingleton<UserService>();
        builder.Services.AddSingleton<ProfileService>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<DropTableService>();
        builder.Services.AddSingleton<SkillService>();
        builder.Services.AddSingleton<ItemService>();
        builder.Services.AddSingleton<ActivityService>();
        builder.Services.AddSingleton<ToolService>();
        builder.Services.AddSingleton(VersionService.FromAssembly());
        builder.Services.AddSingleton<ActivitySchedulerService>();
        builder.Services.AddHostedService<ActivitySchedulerHostedService>();

        WebApplication app = builder.Build();

        app.UseCors();
        app.MapControllers();
        app.MapSocketControllers();
        DropTableData.AddAll(app.Services.GetRequiredService<DropTableService>());
        ActivityData.AddAll(app.Services.GetRequiredService<ActivityService>());
        ToolData.AddAll(app.Services.GetRequiredService<ToolService>());

        return app;
    }

    public static async Task MigrateDatabaseAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();
        IDbContextFactory<GameDbContext> dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDbContext>>();
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
