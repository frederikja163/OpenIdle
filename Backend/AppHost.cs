using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Dtos;
using Backend.Extensions;
using Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
        builder.Services.AddSingleton<UserService>();
        builder.Services.AddSingleton<ProfileService>();
        builder.Services.AddSingleton<DropTableService>();
        builder.Services.AddSingleton<SkillService>();
        builder.Services.AddSingleton<ItemService>();
        builder.Services.AddSingleton<ActivityService>();

        WebApplication app = builder.Build();

        // Cheap liveness signal for the container healthcheck and for the
        // post-deploy check in the publish workflow. Deliberately says nothing
        // about the database: a backend that cannot reach SQLite still needs to
        // come up far enough to be diagnosed rather than be restarted in a loop.
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

        app.MapControllers();
        app.MapSocketControllers();
        DropTableData.AddAll(app.Services.GetRequiredService<DropTableService>());
        ActivityData.AddAll(app.Services.GetRequiredService<ActivityService>());

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
