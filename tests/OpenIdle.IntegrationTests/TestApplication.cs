using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace OpenIdle.IntegrationTests;

public sealed class TestApplication : IDisposable
{
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(30);

    private readonly WebApplication _app;
    private readonly string _dbPath;

    public Uri WsUri { get; }

    public Uri HttpUri { get; }

    public TestApplication()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"openidle-it-{Guid.NewGuid():N}.db");

        WebApplication? app = null;
        try
        {
            using CancellationTokenSource initializationTimeout = new(InitializationTimeout);

            app = AppHost.CreateApp([], $"Data Source={_dbPath};Pooling=False");
            app.Urls.Add("http://127.0.0.1:0");
            AppHost.MigrateDatabaseAsync(app.Services, initializationTimeout.Token).GetAwaiter().GetResult();
            app.StartAsync(initializationTimeout.Token).GetAwaiter().GetResult();

            IServerAddressesFeature addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("Server did not expose its bound addresses.");
            string baseAddress = addresses.Addresses.First(a => a.StartsWith("http://127.0.0.1"));

            _app = app;
            HttpUri = new Uri(baseAddress);
            WsUri = new Uri(baseAddress.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws");
            app = null;
        }
        catch
        {
            if (app is not null)
            {
                try
                {
                    app.StopAsync().GetAwaiter().GetResult();
                }
                catch
                {
                }

                app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            DeleteDatabaseFiles();
            throw;
        }
    }

    public async Task<TestSocketClient> ConnectAsync(CancellationToken externalToken = default)
    {
        return await TestSocketClient.ConnectAsync(WsUri, externalToken).ConfigureAwait(false);
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
            DeleteDatabaseFiles();
        }
    }

    private void DeleteDatabaseFiles()
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
