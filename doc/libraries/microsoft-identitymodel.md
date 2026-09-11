# Microsoft IdentityModel (JWT / OpenID Connect validation)

- Status: adopted
- Date: 2026-09-11
- Decided by: project owner
- Version / commit pinned: 8.22.0 (`Microsoft.IdentityModel.Protocols.OpenIdConnect` and `Microsoft.IdentityModel.JsonWebTokens`)

## 1. Problem

Login is delegated to Zitadel, self-hosted (see [`identity-provider.md`](./identity-provider.md)): the frontend authenticates through Zitadel and receives a signed JWT access token, which it sends to the backend as a socket message (`Login { accessToken }`). The backend must reject any token it did not get from our Zitadel instance, then trust the `sub` claim as the user's identity. Concretely that means, per login: fetch the instance's OpenID Connect discovery document, use it to retrieve and cache the RS256 signing keys (JWKS), and verify the token's signature and its `iss`, `aud`, `exp`, and `nbf` claims.

This is security-critical code. The failure mode of a subtle bug is an authentication bypass — for example trusting the token header's `alg` (the classic `alg: none` / algorithm-confusion attacks), not matching the `kid` to the published key, or not actually validating the issuer. We do not want to own that code.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **`Microsoft.IdentityModel.Protocols.OpenIdConnect` + `Microsoft.IdentityModel.JsonWebTokens`** (chosen) | 8.22.0; 415 KB + 550 KB packages | OIDC discovery + JWKS caching (`ConfigurationManager<OpenIdConnectConfiguration>`) and the modern token validator (`JsonWebTokenHandler`), with **no** ASP.NET HTTP middleware | Microsoft/Entra, first-party; 8.x active and tied to .NET 9 STS & 10 LTS (~Nov 2028); ~6.3M and ~8.0M downloads/day | MIT | **High** — exactly the two primitives needed, no web-request surface |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.12; 67 KB + the same IdentityModel closure | ASP.NET Core JWT middleware: `JwtBearerHandler`, events, DI wiring; validates a token from the `Authorization` header | Microsoft, monthly .NET patch cadence | MIT | Medium — the token arrives as a **socket message**, so the handler never runs; we would pull an HTTP middleware we cannot use |
| `Auth0.AspNetCore.Authentication.Api` (`auth0/aspnetcore-api`) | 1.x; new repo (created 2025-11-18, ~107 commits) | Auth0's resource-server SDK: wraps `JwtBearer`, adds DPoP (RFC 9449) and multiple-custom-domain support | Auth0/Okta; very new and low adoption (single-digit GitHub stars) | MIT | Low — still HTTP-middleware based and still `JwtBearer` underneath; a young third-party wrapper over a first-party stack |
| `Auth0.AspNetCore.Authentication` | 1.11.0 | Server-side OIDC login for MVC/Razor apps: browser redirect + cookie session, token exchange | Auth0/Okta, active | MIT | Low — implements a browser cookie-session flow, not resource-server token validation; wrong shape for a socket protocol |
| `System.IdentityModel.Tokens.Jwt` (legacy) | 8.22.0 | Older JWT handler from the same repo | Microsoft, maintained as legacy | MIT | Low — Microsoft's own guidance is to replace it with `JsonWebTokens` from 7.x on |
| Hand-rolled validation (`System.Security.Cryptography` + `System.Text.Json`) | in-house | Discovery, JWKS cache + rotation, base64url parse, RSA RS256 verify, claim checks | n/a — we maintain it | n/a | Low — security-critical code (alg confusion, `alg:none`, `kid` matching, key rotation) we would own forever |
| Third-party JWT/JOSE library (e.g. `jose-jwt`) | varies | Lightweight JWS/JWE parsing and signing | Community | MIT | Low — no OIDC discovery or JWKS rotation; we would still hand-roll the provider-specific half, plus a non-Microsoft dependency |

Why the others lost: the JWT-bearer middleware and both Auth0 SDKs model an HTTP request/response (`Authorization` header or browser cookie session) that our WebSocket protocol does not have, so their value never materializes. The legacy handler is explicitly superseded by the chosen one. Hand-rolling and the general-purpose JOSE library both leave the provider-specific and security-critical half (discovery, key rotation, claim enforcement) to us, which is the part we are trying not to own.

## 3. Decision & rationale

Adopt the two first-party IdentityModel packages at 8.22.0 and use them directly from a small OIDC validation service — no ASP.NET authentication middleware, no `UseAuthentication`, no cookie services. `ConfigurationManager<OpenIdConnectConfiguration>` fetches the instance's discovery document once and caches the signing keys (refreshing on an interval and on an unknown `kid`); `JsonWebTokenHandler.ValidateTokenAsync` performs the signature and claim validation. Both are released together from the same repository (`AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet`) and are versioned together, so there is a single version to track.

These are the same assemblies that back `Microsoft.AspNetCore.Authentication.JwtBearer`, which validates a large share of Microsoft's own API traffic; we are using them without the HTTP wrapper we do not need.

### Pros

- First-party Microsoft/Entra, MIT-licensed, riding the same LTS window as .NET 10 (8.x supported to roughly November 2028).
- Handles the hard, security-critical parts: OIDC discovery, JWKS caching and rotation, signature verification, algorithm/padding hardening, and lifetime/issuer/audience checks.
- Smallest option that covers the need — two packages and no web-request machinery; the alternatives that bundle HTTP middleware provide nothing extra for a socket-message token.
- Extremely well-exercised: it is the validation engine behind ASP.NET Core's JWT bearer authentication (~6–9M downloads/day for the chosen packages).
- One version pin for the whole stack; straightforward to audit and upgrade.

### Cons

- Pulls in a closure of seven Microsoft assemblies (`OpenIdConnect`, `Protocols`, `System.IdentityModel.Tokens.Jwt`, `JsonWebTokens`, `Tokens`, `Logging`, `Abstractions`) — under 1 MB of runtime assemblies — for a single login check per connection.
- Introduces an outbound HTTPS call to the provider's discovery endpoint (on first use, then cached). An instance/network that cannot reach `{issuer}/.well-known/openid-configuration` cannot validate logins; validation itself stays local once the keys are cached.
- A second identity-related package family in the dependency tree beyond the platform itself, even if first-party.
- The two packages must be kept on the same major/minor version (mixing 7.x and 8.x is unsupported).

## 4. Build-vs-buy

The in-house alternative is a few hundred lines — discovery fetch, a JWKS cache with `kid` selection and rotation, base64url decoding, RSA RS256 verification via `System.Security.Cryptography`, and claim checks — plus the tests that prove each rejection path. Call it one to two focused days. That is close enough to the skill's "hours, not weeks" line that it deserves a real answer, and the answer is the nature of the code rather than its size: this is the security-critical authentication edge, where a single mistake (trusting the header algorithm, skipping `kid` matching, not enforcing `iss`/`aud`) is an authentication bypass. It is the same reasoning this project already recorded for ASP.NET Core itself — "it's the security-critical network edge where bugs cost players' data." The library is first-party, already inside the ASP.NET Core orbit, and maintained on the platform's patch cadence, so the buy case is strong.

## 5. Risk

### Undo risk — low

The integration is scoped to a single validation service, one options type and one config section, so replacing the library later means reimplementing one method; nothing else in the codebase needs to reference the IdentityModel types. The version is pinned at 8.22.0 in the header and will be applied in `Backend.csproj` when the integration lands.

### Security risk — low

First-party Microsoft, MIT-licensed, patched on the monthly .NET cadence, no install scripts and no native binaries. The repository's historical advisories do not affect this pin: CVE-2024-21643 (SignedHttpRequest JKU fetch) affects only the SignedHttpRequest protocol and was fixed in 7.1.2 / 6.34.0, and the JWE decompression denial-of-service (CVE-2024-21319) was fixed in the same range — both are far below 8.22.0 and off the code paths used here. The 8.x line is the currently supported one. The main residual risk is ours: we must configure `TokenValidationParameters` to pin a single issuer and audience and to reject unexpected algorithms (the defaults do pin the algorithm from the signing key), and keep the package updated as part of .NET servicing.
