using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backend;

namespace OpenIdle.IntegrationTests;

public sealed class LevelsHttpIntegrationTests : IDisposable
{
    private readonly TestApplication _app;
    private readonly HttpClient _http;

    public LevelsHttpIntegrationTests()
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
    public async Task GetLevels_ReportsTheCumulativeCurve(CancellationToken ct)
    {
        HttpResponseMessage response = await _http.GetAsync("/levels", ct).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        JsonElement levels = body.RootElement;

        Assert.Multiple(() =>
        {
            // Levels 0..MaxLevel, plus one past the cap for the final level's span.
            Assert.That(levels.GetArrayLength(), Is.EqualTo(LevelCurve.MaxLevel + 2));
            Assert.That(levels[0].GetInt32(), Is.EqualTo(0));
            Assert.That(levels[1].GetInt32(), Is.EqualTo(895));
            Assert.That(levels[LevelCurve.MaxLevel].GetInt32(), Is.EqualTo(3096260));
        });
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task GetLevels_AnswersAnyOrigin(CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/levels");
        request.Headers.Add("Origin", "https://evil.example");
        HttpResponseMessage response = await _http.SendAsync(request, ct).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { "*" }));
    }
}
