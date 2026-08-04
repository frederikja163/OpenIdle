using System;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Extensions;
using Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

WebApplication app = Program.CreateApp(args);
await Program.MigrateDatabaseAsync(app.Services);
app.Run();

internal partial class Program
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

        WebApplication app = builder.Build();
        app.MapControllers();
        app.MapSocketControllers();

        return app;
    }

    public static async Task MigrateDatabaseAsync(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        IDbContextFactory<GameDbContext> dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDbContext>>();
        await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();
    }
}
