using Backend.Database;
using Backend.Extensions;
using Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddDbContext<GameDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers();
builder.Services.AddSocketControllers();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<ProfileService>();

var app = builder.Build();

app.MapControllers();
app.MapSocketControllers();

app.Run();