using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenIdle.IntegrationTests;

public sealed class VersionHttpIntegrationTests : IDisposable
{
    private const string AllowedOrigin = "https://allowed.example";
    private const string ForeignOrigin = "https://evil.example";

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

    [Test]
    [CancelAfter(30_000)]
    public async Task GetVersion_AnswersAnyOrigin(CancellationToken ct)
    {
        HttpResponseMessage response = await GetVersionFromOriginAsync(_http, ForeignOrigin, ct).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { "*" }));
    }

    /// <summary>
    /// The HTTP side is public even when the socket is not: AllowedWsOrigins gates
    /// the handshake only, and never narrows what a browser may read over HTTP.
    /// </summary>
    [Test]
    [CancelAfter(30_000)]
    public async Task AllowedWsOrigins_GatesTheSocketButNotHttp(CancellationToken ct)
    {
        using TestApplication restricted = new($"--AllowedWsOrigins:0={AllowedOrigin}");
        using HttpClient http = new() { BaseAddress = restricted.HttpUri };

        HttpResponseMessage response = await GetVersionFromOriginAsync(http, ForeignOrigin, ct).ConfigureAwait(false);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { "*" }));

        using ClientWebSocket foreignSocket = new();
        foreignSocket.Options.SetRequestHeader("Origin", ForeignOrigin);
        WebSocketException? rejection = Assert.ThrowsAsync<WebSocketException>(
            () => foreignSocket.ConnectAsync(restricted.WsUri, ct));
        Assert.That(rejection!.Message, Does.Contain("403"));

        using ClientWebSocket allowedSocket = new();
        allowedSocket.Options.SetRequestHeader("Origin", AllowedOrigin);
        await allowedSocket.ConnectAsync(restricted.WsUri, ct).ConfigureAwait(false);
        Assert.That(allowedSocket.State, Is.EqualTo(WebSocketState.Open));
    }

    private static async Task<HttpResponseMessage> GetVersionFromOriginAsync(HttpClient http, string origin, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/version");
        request.Headers.Add("Origin", origin);
        return await http.SendAsync(request, ct).ConfigureAwait(false);
    }
}
