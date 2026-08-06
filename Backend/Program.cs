using Backend.Database;
using Backend.Dtos;
using Backend.Extensions;
using Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddDbContextFactory<GameDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers();
builder.Services.AddSocketControllers();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<ProfileService>();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    IDbContextFactory<GameDbContext> dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDbContext>>();
    await using GameDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
    await dbContext.Database.MigrateAsync();
}

app.MapControllers();
app.MapSocketControllers();

app.Run();