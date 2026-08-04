using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace OpenIdle.IntegrationTests;

public sealed class TestApplication : IDisposable
{
    private readonly WebApplication _app;
    private readonly string _dbPath;

    public Uri WsUri { get; }

    public TestApplication()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"openidle-it-{Guid.NewGuid():N}.db");

        WebApplication app = Program.CreateApp([], $"Data Source={_dbPath};Pooling=False");
        app.Urls.Add("http://127.0.0.1:0");
        Program.MigrateDatabaseAsync(app.Services).GetAwaiter().GetResult();
        app.StartAsync().GetAwaiter().GetResult();

        IServerAddressesFeature addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Server did not expose its bound addresses.");
        string baseAddress = addresses.Addresses.First(a => a.StartsWith("http://127.0.0.1"));

        _app = app;
        WsUri = new Uri(baseAddress.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws");
    }

    public async Task<TestSocket> ConnectAsync(CancellationToken externalToken = default)
    {
        TestSocket socket = new(WsUri);
        await socket.ConnectAsync(externalToken).ConfigureAwait(false);
        return socket;
    }

    public void Dispose()
    {
        try
        {
            _app.StopAsync().GetAwaiter().GetResult();
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            foreach (string file in new[] { _dbPath, $"{_dbPath}-shm", $"{_dbPath}-wal" })
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        File.Delete(file);
                        break;
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }

                    Thread.Sleep(100);
                }
            }
        }
    }
}
