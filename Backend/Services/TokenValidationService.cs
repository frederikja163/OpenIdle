using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Services;

public sealed class TokenValidationService
{
    private readonly AuthOptions _options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    public TokenValidationService(IOptions<AuthOptions> options)
        : this(options, new HttpDocumentRetriever())
    {
    }

    internal TokenValidationService(IOptions<AuthOptions> options, IDocumentRetriever documentRetriever)
    {
        _options = options.Value;

        string metadataAddress = $"{_options.Issuer.TrimEnd('/')}/.well-known/openid-configuration";
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            documentRetriever);
    }

    internal async Task<string> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Issuer) || string.IsNullOrWhiteSpace(_options.Audience))
        {
            throw new BackendException("Authentication is not configured on this server.");
        }

        OpenIdConnectConfiguration configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);

        TokenValidationParameters parameters = new()
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.Zero,
            ConfigurationManager = _configurationManager,
        };

        TokenValidationResult result = await _handler.ValidateTokenAsync(accessToken, parameters);
        if (!result.IsValid)
        {
            Log.Warning($"Rejected access token: {result.Exception?.Message}");
            throw new BackendException("Invalid or expired access token.");
        }

        string? subject = result.ClaimsIdentity.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            throw new BackendException("Access token is missing a subject.");
        }

        return subject;
    }
}
