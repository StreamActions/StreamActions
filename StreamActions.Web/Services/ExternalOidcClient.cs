/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Common;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.Collections.Specialized;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StreamActions.Web.Services;

/// <summary>
/// Implements OpenID Connect discovery, PKCE (<c>S256</c>) authorization requests, and ID token validation for <see cref="ExternalOidcProvider"/> accounts.
/// </summary>
public sealed class ExternalOidcClient : IExternalOidcClient
{
    /// <summary>
    /// Named HTTP client identifier used with <see cref="IHttpClientFactory"/>.
    /// </summary>
    public const string HttpClientName = "ExternalOidc";

    /// <summary>
    /// Default scopes requested when a provider does not explicitly configure any scopes.
    /// </summary>
    private static readonly string[] _defaultScopes = ["openid", "profile", "email"];

    /// <summary>
    /// Optional HTTP client factory for outbound OIDC discovery and token requests.
    /// </summary>
    private readonly IHttpClientFactory? _httpClientFactory;

    /// <summary>
    /// Optional explicit HTTP client instance used in unit or integration tests.
    /// </summary>
    private readonly HttpClient? _explicitHttpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalOidcClient"/> class using <see cref="IHttpClientFactory"/>.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    [ActivatorUtilitiesConstructor]
    public ExternalOidcClient(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this._httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalOidcClient"/> class using an explicit <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for discovery and token requests.</param>
    public ExternalOidcClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this._explicitHttpClient = httpClient;
    }

    /// <inheritdoc />
    public async ValueTask<ExternalOidcAuthorizationRequest> BuildAuthorizationRequestAsync(
        ExternalOidcProvider provider,
        Uri redirectUri,
        string? state = null,
        string? nonce = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.AuthorityUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.ClientId);

        OpenIdConnectConfiguration config = await this.GetConfigurationAsync(provider.AuthorityUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(config.AuthorizationEndpoint))
        {
            throw new InvalidOperationException("OIDC discovery document did not include an authorization_endpoint.");
        }

        string resolvedState = string.IsNullOrWhiteSpace(state) ? GenerateRandomToken() : state.Trim();
        string resolvedNonce = string.IsNullOrWhiteSpace(nonce) ? GenerateRandomToken() : nonce.Trim();
        string codeVerifier = GenerateRandomToken();
        string codeChallenge = ComputeS256Challenge(codeVerifier);
        List<string> scopes = ParseScopes(provider.ScopesJson);

        NameValueCollection queryParameters = new()
        {
            { "response_type", "code" },
            { "client_id", provider.ClientId.Trim() },
            { "redirect_uri", redirectUri.ToString() },
            { "scope", string.Join(' ', scopes) },
            { "state", resolvedState },
            { "nonce", resolvedNonce },
            { "code_challenge", codeChallenge },
            { "code_challenge_method", "S256" },
        };

        Uri authorizationUri = Util.BuildUri(new(config.AuthorizationEndpoint), queryParameters);
        return new ExternalOidcAuthorizationRequest(
            authorizationUri,
            resolvedState,
            resolvedNonce,
            codeVerifier,
            redirectUri);
    }

    /// <inheritdoc />
    public async ValueTask<ExternalOidcAuthenticationResult> ExchangeCodeAsync(
        ExternalOidcProvider provider,
        string clientSecret,
        string code,
        Uri redirectUri,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.AuthorityUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedNonce);

        OpenIdConnectConfiguration config = await this.GetConfigurationAsync(provider.AuthorityUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(config.TokenEndpoint))
        {
            throw new InvalidOperationException("OIDC discovery document did not include a token_endpoint.");
        }

        string? idToken = await this.ExchangeAuthorizationCodeForIdTokenAsync(
            config.TokenEndpoint,
            provider.ClientId.Trim(),
            clientSecret,
            code,
            redirectUri,
            codeVerifier,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new SecurityTokenException("OIDC token response did not include an id_token.");
        }

        JsonWebTokenHandler tokenHandler = new();
        TokenValidationParameters validationParameters = new()
        {
            IssuerSigningKeys = config.SigningKeys,
            ValidIssuer = config.Issuer,
            ValidAudience = provider.ClientId.Trim(),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
        };

        TokenValidationResult validationResult = await tokenHandler
            .ValidateTokenAsync(idToken, validationParameters)
            .ConfigureAwait(false);

        if (!validationResult.IsValid || validationResult.SecurityToken is not JsonWebToken jwt)
        {
            throw new SecurityTokenException("OIDC ID token validation failed.", validationResult.Exception);
        }

        if (!jwt.TryGetClaim("nonce", out Claim? nonceClaim)
            || !string.Equals(nonceClaim.Value, expectedNonce, StringComparison.Ordinal))
        {
            throw new SecurityTokenException("OIDC ID token nonce mismatch.");
        }

        string? subject = jwt.Subject;
        if (string.IsNullOrWhiteSpace(subject) && jwt.TryGetClaim("sub", out Claim? subClaim))
        {
            subject = subClaim.Value;
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new SecurityTokenException("OIDC ID token is missing the required 'sub' claim.");
        }

        string? preferredUsername = GetOptionalClaim(jwt, "preferred_username");
        string? nickname = GetOptionalClaim(jwt, "nickname");
        string? name = GetOptionalClaim(jwt, "name");
        string? email = GetOptionalClaim(jwt, "email");

        string username = preferredUsername ?? nickname ?? name ?? subject;
        string displayName = name ?? preferredUsername ?? username;
        List<string> groups = ExtractGroups(jwt, provider.GroupClaimType);

        return new ExternalOidcAuthenticationResult(
            subject,
            username,
            displayName,
            email,
            groups);
    }

    /// <summary>
    /// Generates a cryptographically random URL-safe token string.
    /// </summary>
    /// <returns>A base64url-encoded 256-bit random token.</returns>
    private static string GenerateRandomToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Computes an RFC 7636 PKCE <c>S256</c> code challenge from a code verifier.
    /// </summary>
    /// <param name="codeVerifier">The PKCE code verifier.</param>
    /// <returns>The base64url-encoded SHA-256 hash of <paramref name="codeVerifier"/>.</returns>
    private static string ComputeS256Challenge(string codeVerifier) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    /// <summary>
    /// Parses the provider's configured scopes JSON array, ensuring <c>openid</c> is always included.
    /// </summary>
    /// <param name="scopesJson">The serialized JSON array of scopes.</param>
    /// <returns>The normalized list of scopes to request.</returns>
    private static List<string> ParseScopes(string? scopesJson)
    {
        List<string> scopes = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(scopesJson))
        {
            try
            {
                string[]? parsed = JsonSerializer.Deserialize<string[]>(scopesJson);
                if (parsed is not null)
                {
                    foreach (string rawScope in parsed)
                    {
                        if (string.IsNullOrWhiteSpace(rawScope))
                        {
                            continue;
                        }

                        string trimmed = rawScope.Trim();
                        if (seen.Add(trimmed))
                        {
                            scopes.Add(trimmed);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to default scopes when malformed JSON is encountered.
            }
        }

        if (scopes.Count == 0)
        {
            foreach (string defaultScope in _defaultScopes)
            {
                if (seen.Add(defaultScope))
                {
                    scopes.Add(defaultScope);
                }
            }
        }
        else if (!seen.Contains("openid"))
        {
            scopes.Insert(0, "openid");
        }

        return scopes;
    }

    /// <summary>
    /// Retrieves an optional non-whitespace claim value from a validated <see cref="JsonWebToken"/>.
    /// </summary>
    /// <param name="jwt">The validated JWT.</param>
    /// <param name="claimType">The claim type name.</param>
    /// <returns>The trimmed claim value, or <see langword="null"/> if absent or blank.</returns>
    private static string? GetOptionalClaim(JsonWebToken jwt, string claimType) =>
        jwt.TryGetClaim(claimType, out Claim? claim) && !string.IsNullOrWhiteSpace(claim.Value)
            ? claim.Value.Trim()
            : null;

    /// <summary>
    /// Extracts group values from the specified claim type, supporting both JSON arrays and single string values.
    /// </summary>
    /// <param name="jwt">The validated JWT.</param>
    /// <param name="groupClaimType">The configured group claim type, or <see langword="null"/>.</param>
    /// <returns>A deduplicated list of group names.</returns>
    private static List<string> ExtractGroups(JsonWebToken jwt, string? groupClaimType)
    {
        if (string.IsNullOrWhiteSpace(groupClaimType))
        {
            return [];
        }

        string claimType = groupClaimType.Trim();
        List<string> groups = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        if (jwt.TryGetPayloadValue(claimType, out JsonElement element))
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddGroupValue(item.GetString(), groups, seen);
                    }
                }

                return groups;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                AddSingleOrSerializedArray(element.GetString(), groups, seen);
                return groups;
            }
        }

        foreach (Claim claim in jwt.Claims)
        {
            if (string.Equals(claim.Type, claimType, StringComparison.Ordinal))
            {
                AddSingleOrSerializedArray(claim.Value, groups, seen);
            }
        }

        return groups;
    }

    /// <summary>
    /// Parses a string claim value as either a JSON string array or a single group name.
    /// </summary>
    /// <param name="rawValue">The raw string claim value.</param>
    /// <param name="groups">The destination group list.</param>
    /// <param name="seen">A case-insensitive deduplication set.</param>
    private static void AddSingleOrSerializedArray(string? rawValue, List<string> groups, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        string trimmed = rawValue.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            try
            {
                string[]? parsed = JsonSerializer.Deserialize<string[]>(trimmed);
                if (parsed is not null)
                {
                    foreach (string item in parsed)
                    {
                        AddGroupValue(item, groups, seen);
                    }

                    return;
                }
            }
            catch (JsonException)
            {
                // Treat as a literal group name if not valid JSON.
            }
        }

        AddGroupValue(trimmed, groups, seen);
    }

    /// <summary>
    /// Trims and adds a non-empty group value if not already present.
    /// </summary>
    /// <param name="value">The candidate group name.</param>
    /// <param name="groups">The destination group list.</param>
    /// <param name="seen">A case-insensitive deduplication set.</param>
    private static void AddGroupValue(string? value, List<string> groups, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        if (seen.Add(trimmed))
        {
            groups.Add(trimmed);
        }
    }

    /// <summary>
    /// Validates the authority URL and retrieves the OpenID Connect discovery configuration.
    /// </summary>
    /// <param name="authorityUrl">The OIDC authority base URL or discovery URL.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved <see cref="OpenIdConnectConfiguration"/>.</returns>
    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string authorityUrl,
        CancellationToken cancellationToken)
    {
        Uri authorityUri = new(authorityUrl.Trim(), UriKind.Absolute);
        bool isHttps = string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttps && !authorityUri.IsLoopback)
        {
            throw new InvalidOperationException("External OIDC authority URL must use HTTPS unless targeting a loopback address.");
        }

        string discoveryUrl = authorityUri.AbsoluteUri.EndsWith("/.well-known/openid-configuration", StringComparison.OrdinalIgnoreCase)
            ? authorityUri.AbsoluteUri
            : $"{authorityUri.AbsoluteUri.TrimEnd('/')}/.well-known/openid-configuration";

        (HttpClient httpClient, bool shouldDispose) = this.AcquireHttpClient();
        try
        {
            HttpDocumentRetriever documentRetriever = new(httpClient)
            {
                RequireHttps = !authorityUri.IsLoopback,
            };

            return await OpenIdConnectConfigurationRetriever
                .GetAsync(discoveryUrl, documentRetriever, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (shouldDispose)
            {
                httpClient.Dispose();
            }
        }
    }

    /// <summary>
    /// Posts an authorization code exchange request with PKCE verifier to the OIDC token endpoint.
    /// </summary>
    /// <param name="tokenEndpoint">The discovered token endpoint URL.</param>
    /// <param name="clientId">The OIDC client identifier.</param>
    /// <param name="clientSecret">The OIDC client secret.</param>
    /// <param name="code">The authorization code.</param>
    /// <param name="redirectUri">The redirect URI used in the authorization request.</param>
    /// <param name="codeVerifier">The PKCE code verifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The returned <c>id_token</c> string, or <see langword="null"/> if absent.</returns>
    private async Task<string?> ExchangeAuthorizationCodeForIdTokenAsync(
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        string code,
        Uri redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        (HttpClient httpClient, bool shouldDispose) = this.AcquireHttpClient();
        try
        {
            using FormUrlEncodedContent formContent = new(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri.ToString() },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "code_verifier", codeVerifier },
            });

            using HttpResponseMessage response = await httpClient
                .PostAsync(new Uri(tokenEndpoint, UriKind.Absolute), formContent, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"OIDC token exchange failed with HTTP status {(int)response.StatusCode}.");
            }

            string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("id_token", out JsonElement idTokenElement)
                && idTokenElement.ValueKind == JsonValueKind.String
                ? idTokenElement.GetString()
                : null;
        }
        finally
        {
            if (shouldDispose)
            {
                httpClient.Dispose();
            }
        }
    }

    /// <summary>
    /// Obtains an <see cref="HttpClient"/> from either the explicit instance or <see cref="IHttpClientFactory"/>.
    /// </summary>
    /// <returns>The <see cref="HttpClient"/> and a boolean indicating whether the caller must dispose it.</returns>
    private (HttpClient Client, bool ShouldDispose) AcquireHttpClient() =>
        this._explicitHttpClient is not null
            ? (this._explicitHttpClient, false)
            : (this._httpClientFactory!.CreateClient(HttpClientName), true);
}
