using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenIdle.IntegrationTests;

public sealed class VersionHttpIntegrationTests : IDisposable
{
    private readonly TestApplication _app;
    private readonly HttpClient _http;

    public VersionHttpIntegrationTests()
    {
        _app = new TestApplication();
        _http = new HttpClient { BaseAddress = _app.HttpUri };
    }

    public void Dispose()
    {
        _http.Dispose();
        _app.Dispose();
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task GetVersion_ReportsAnEmptyBuild(CancellationToken ct)
    {
        HttpResponseMessage response = await _http.GetAsync("/version", ct).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        Assert.That(body.RootElement.GetProperty("commit").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(body.RootElement.GetProperty("commitTime").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }
}