using Backend.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSocketControllers();

var app = builder.Build();

app.MapControllers();
app.MapSocketControllers();
app.UseHttpsRedirection();

app.Run();