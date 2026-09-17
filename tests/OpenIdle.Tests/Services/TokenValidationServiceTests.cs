using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backend;
using Backend.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace OpenIdle.Tests.Services;

public sealed class TokenValidationServiceTests
{
    private const string Issuer = "https://test-issuer";
    private const string Audience = "test-audience";
    private const string KeyId = "test-key";

    private static RsaSecurityKey CreateSigningKey()
    {
        return new RsaSecurityKey(RSA.Create(2048)) { KeyId = KeyId };
    }

    private static string CreateToken(RsaSecurityKey signingKey, string issuer = Issuer, string audience = Audience,
        string? subject = "user-123", DateTime? expires = null)
    {
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = issuer,
            Audience = audience,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
        };
        if (subject is not null)
        {
            descriptor.Claims = new Dictionary<string, object> { ["sub"] = subject };
        }

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static TokenValidationService CreateService(RsaSecurityKey signingKey)
    {
        JsonWebKey publicKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(signingKey);
        string jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new { kty = publicKey.Kty, kid = publicKey.Kid, n = publicKey.N, e = publicKey.E },
            },
        });
        string discovery = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["issuer"] = Issuer,
            ["jwks_uri"] = $"{Issuer}/jwks",
        });

        FakeDocumentRetriever retriever = new(new Dictionary<string, string>
        {
            [$"{Issuer}/.well-known/openid-configuration"] = discovery,
            [$"{Issuer}/jwks"] = jwks,
        });

        return new TokenValidationService(
            Options.Create(new AuthOptions { Issuer = Issuer, Audience = Audience }),
            retriever);
    }

    [Test]
    public async Task ValidToken_ReturnsSubject()
    {
        RsaSecurityKey key = CreateSigningKey();
        TokenValidationService service = CreateService(key);

        string subject = await service.ValidateAsync(CreateToken(key), CancellationToken.None);

        Assert.That(subject, Is.EqualTo("user-123"));
    }

    [Test]
    public void TokenSignedByUnknownKey_IsRejected()
    {
        TokenValidationService service = CreateService(CreateSigningKey());
        RsaSecurityKey attackerKey = CreateSigningKey();

        Assert.ThrowsAsync<BackendException>(() => service.ValidateAsync(CreateToken(attackerKey), CancellationToken.None));
    }

    [Test]
    public void TokenWithWrongAudience_IsRejected()
    {
        RsaSecurityKey key = CreateSigningKey();
        TokenValidationService service = CreateService(key);

        Assert.ThrowsAsync<BackendException>(() =>
            service.ValidateAsync(CreateToken(key, audience: "some-other-client"), CancellationToken.None));
    }

    [Test]
    public void TokenFromWrongIssuer_IsRejected()
    {
        RsaSecurityKey key = CreateSigningKey();
        TokenValidationService service = CreateService(key);

        Assert.ThrowsAsync<BackendException>(() =>
            service.ValidateAsync(CreateToken(key, issuer: "https://evil.example"), CancellationToken.None));
    }

    [Test]
    public void ExpiredToken_IsRejected()
    {
        RsaSecurityKey key = CreateSigningKey();
        TokenValidationService service = CreateService(key);

        Assert.ThrowsAsync<BackendException>(() =>
            service.ValidateAsync(CreateToken(key, expires: DateTime.UtcNow.AddSeconds(-5)), CancellationToken.None));
    }

    [Test]
    public void TokenWithoutSubject_IsRejected()
    {
        RsaSecurityKey key = CreateSigningKey();
        TokenValidationService service = CreateService(key);

        Assert.ThrowsAsync<BackendException>(() =>
            service.ValidateAsync(CreateToken(key, subject: null), CancellationToken.None));
    }

    [Test]
    public async Task UnconfiguredAuth_IsRejected()
    {
        TokenValidationService service = new(
            Options.Create(new AuthOptions()),
            new FakeDocumentRetriever([]));

        Assert.ThrowsAsync<BackendException>(() =>
            service.ValidateAsync("any-token", CancellationToken.None));
    }

    private sealed class FakeDocumentRetriever(Dictionary<string, string> documents) : IDocumentRetriever
    {
        public Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            return Task.FromResult(documents[address]);
        }
    }
}
