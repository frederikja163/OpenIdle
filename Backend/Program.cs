using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateSlimBuilder(args);


var app = builder.Build();

app.UseHttpsRedirection();
app.UseWebSockets();

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest("Expected a WebSocket request to /ws");
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

    var buffer = new byte[4096];
    var receive = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

    while (!receive.CloseStatus.HasValue)
    {
        await webSocket.SendAsync(
            buffer.AsMemory(0, receive.Count),
            receive.MessageType,
            receive.EndOfMessage,
            CancellationToken.None);

        receive = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
    }

    await webSocket.CloseAsync(
        receive.CloseStatus.Value,
        receive.CloseStatusDescription,
        CancellationToken.None);

    return Results.Empty;
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}