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

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Common.Attributes;
using StreamActions.Common.Extensions;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.Common;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.OAuth;

/// <summary>
/// Record containing response data for the Token endpoint.
/// </summary>
[DocParser("Authorize OAuth", 82, "https://dev.twitch.tv/docs/authentication/getting-tokens-oauth", "twitch/TwitchOAuthParser")]
[DocParser("Refresh OAuth", 82, "https://dev.twitch.tv/docs/authentication/refresh-tokens", "twitch/TwitchRefreshParser")]
[DocParser("Revoke OAuth", 82, "https://dev.twitch.tv/docs/authentication/revoke-tokens", "twitch/TwitchRevokeParser")]
public sealed record Token : JsonApiResponse
{

    /// <summary>
    /// The OAuth token that has been granted.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    /// <summary>
    /// The refresh token associated with the <see cref="AccessToken"/>.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>
    /// The approximate DateTime that <see cref="ExpiresIn"/> refers to.
    /// </summary>
    [field: JsonIgnore]
    [JsonIgnore]
    public DateTime Expires { get => field.AddSeconds(this.ExpiresIn); } = TimeProvider.System.GetUtcNow().UtcDateTime;

    /// <summary>
    /// The number of seconds, in server time, until the OAuth token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    /// <summary>
    /// The scopes that are authorized under the OAuth token.
    /// </summary>
    [JsonPropertyName("scope")]
    public IReadOnlyList<Scope>? Scopes { get; init; }

    /// <summary>
    /// The OIDC JWT token string, if authorizing with the <see cref="Scope.OpenID"/> scope.
    /// </summary>
    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    /// <summary>
    /// The type of token that <see cref="AccessToken"/> represents.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    /// <summary>
    /// Formats the record members into the specified <see cref="StringBuilder"/>, redacting sensitive token values.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (base.PrintMembers(builder))
        {
            _ = builder.Append(", ");
        }

        _ = builder.Append(nameof(this.AccessToken)).Append(" = ").Append(string.IsNullOrEmpty(this.AccessToken) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.RefreshToken)).Append(" = ").Append(string.IsNullOrEmpty(this.RefreshToken) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.Expires)).Append(" = ").Append(this.Expires);
        _ = builder.Append(", ").Append(nameof(this.ExpiresIn)).Append(" = ").Append(this.ExpiresIn);
        _ = builder.Append(", ").Append(nameof(this.Scopes)).Append(" = ").Append(this.Scopes);
        _ = builder.Append(", ").Append(nameof(this.IdToken)).Append(" = ").Append(string.IsNullOrEmpty(this.IdToken) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.TokenType)).Append(" = ").Append(this.TokenType);
        return true;
    }

    /// <summary>
    /// Attempts to refresh the User OAuth token assigned to the specified <see cref="TwitchSession"/>.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to refresh.</param>
    /// <param name="baseAddress">The uri to the Token endpoint. <see langword="null"/> to read from OpenID Connect configuration.</param>
    /// <returns>A <see cref="Token"/> with the new token data or a Twitch error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="session"/> does not have a valid refresh token.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static async Task<Token?> RefreshOAuth(TwitchSession session, string? baseAddress = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.RequireUserToken();

        if (string.IsNullOrWhiteSpace(session.Token?.Refresh))
        {
            throw new InvalidOperationException(nameof(TwitchToken.Refresh) + " is not a valid refresh token");
        }

        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            OpenIdConnectConfiguration config = await GetOpenIdConnectConfigurationAsync(cancellationToken: session.CancellationToken).ConfigureAwait(false);
            baseAddress = config.TokenEndpoint;
        }

        using FormUrlEncodedContent content = new(new Dictionary<string, string>() { { "grant_type", "refresh_token" }, { "refresh_token", session.Token.Refresh ?? "" },
            { "client_id", session.CustomClientId ?? TwitchApi.ClientId ?? "" }, { "client_secret", session.CustomClientSecret ?? TwitchApi.ClientSecret ?? "" } });
        using TwitchSession requestSession = CreateTokenRequestSession(session);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new(baseAddress), requestSession, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<Token>(TwitchApi.SerializerOptions, session.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to authorize a new User OAuth token using an authorization code.
    /// </summary>
    /// <param name="code">The authorization code.</param>
    /// <param name="redirectUri">The redirect Uri that was used.</param>
    /// <param name="baseAddress">The uri to the Token endpoint. <see langword="null"/> to read from OpenID Connect configuration.</param>
    /// <returns>A <see cref="Token"/> with the new token data or a Twitch error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/> or whitespace, or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
    public static Task<Token?> AuthorizeOAuth(string code, Uri redirectUri, string? baseAddress = null) => AuthorizeOAuth(code, redirectUri, session: null, baseAddress);

    /// <summary>
    /// Attempts to authorize a new User OAuth token using an authorization code and optional per-session overrides.
    /// </summary>
    /// <param name="code">The authorization code.</param>
    /// <param name="redirectUri">The redirect Uri that was used.</param>
    /// <param name="session">Optional <see cref="TwitchSession"/> providing custom client credentials or HTTP overrides.</param>
    /// <param name="baseAddress">The uri to the Token endpoint. <see langword="null"/> to read from OpenID Connect configuration.</param>
    /// <returns>A <see cref="Token"/> with the new token data or a Twitch error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/> or whitespace, or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
    public static async Task<Token?> AuthorizeOAuth(string code, Uri redirectUri, TwitchSession? session, string? baseAddress = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentNullException(nameof(code));
        }

        ArgumentNullException.ThrowIfNull(redirectUri);

        CancellationToken cancellationToken = session?.CancellationToken ?? CancellationToken.None;
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            OpenIdConnectConfiguration config = await GetOpenIdConnectConfigurationAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            baseAddress = config.TokenEndpoint;
        }

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "client_id", session?.CustomClientId ?? TwitchApi.ClientId ?? "" },
            { "client_secret", session?.CustomClientSecret ?? TwitchApi.ClientSecret ?? "" },
            { "redirect_uri", redirectUri.ToString() }
        });
        using TwitchSession? requestSession = session is not null ? CreateTokenRequestSession(session) : null;
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new(baseAddress), requestSession ?? TwitchSession.Empty, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<Token>(TwitchApi.SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to authorize a new App OAuth token.
    /// </summary>
    /// <param name="baseAddress">The uri to the Token endpoint. <see langword="null"/> to read from OpenID Connect configuration.</param>
    /// <param name="session">Optional <see cref="TwitchSession"/> providing custom client credentials or HTTP overrides.</param>
    /// <returns>A <see cref="Token"/> with the new token data or a Twitch error.</returns>
    public static async Task<Token?> AuthorizeApp(string? baseAddress = null, TwitchSession? session = null)
    {
        CancellationToken cancellationToken = session?.CancellationToken ?? CancellationToken.None;
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            OpenIdConnectConfiguration config = await GetOpenIdConnectConfigurationAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            baseAddress = config.TokenEndpoint;
        }

        using FormUrlEncodedContent content = new(new Dictionary<string, string>{ { "grant_type", "client_credentials" },
            { "client_id", session?.CustomClientId ?? TwitchApi.ClientId ?? "" }, { "client_secret", session?.CustomClientSecret ?? TwitchApi.ClientSecret ?? "" } });
        using TwitchSession? requestSession = session is not null ? CreateTokenRequestSession(session) : null;
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new(baseAddress), requestSession ?? TwitchSession.Empty, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<Token>(TwitchApi.SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Revokes the OAuth token.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to revoke.</param>
    /// <param name="baseAddress">The uri to the Revoke endpoint.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static async Task<JsonApiResponse?> RevokeOAuth(TwitchSession session, string baseAddress = "https://id.twitch.tv/oauth2/revoke")
    {
        ArgumentNullException.ThrowIfNull(session);

        session.RequireUserToken();

        using FormUrlEncodedContent content = new(new Dictionary<string, string>() { { "client_id", session.CustomClientId ?? TwitchApi.ClientId ?? "" }, { "token", session.Token?.OAuth ?? "" } });
        using TwitchSession requestSession = CreateTokenRequestSession(session);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new(baseAddress), requestSession, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions, session.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an empty-token <see cref="TwitchSession"/> that preserves per-session client and HTTP overrides for OAuth token requests.
    /// </summary>
    /// <param name="session">The source <see cref="TwitchSession"/>.</param>
    /// <returns>A new <see cref="TwitchSession"/> with <see cref="TwitchToken.Empty"/>.</returns>
    internal static TwitchSession CreateTokenRequestSession(TwitchSession session) => new()
    {
        Token = TwitchToken.Empty,
        CancellationToken = session.CancellationToken,
        CustomBaseUri = session.CustomBaseUri,
        CustomClientId = session.CustomClientId,
        CustomClientSecret = session.CustomClientSecret,
        CustomHttpClient = session.CustomHttpClient,
        RateLimiter = session.RateLimiter,
        TimeProvider = session.TimeProvider
    };

    /// <summary>
    /// The default OpenID Connect configuration URL.
    /// </summary>
    private const string _defaultOpenIdConnectConfigurationUrl = "https://id.twitch.tv/oauth2/.well-known/openid-configuration";

    /// <summary>
    /// Cached OpenID Connect configuration managers keyed by metadata address when using default HTTP transport.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _configurationManagers = new(StringComparer.Ordinal);

    /// <summary>
    /// The active default OpenID Connect metadata address.
    /// </summary>
    private static string _defaultOpenIdConnectMetadataAddress = _defaultOpenIdConnectConfigurationUrl;

    /// <summary>
    /// The default <see cref="ConfigurationManager{T}"/> for retrieving OpenID Connect discovery and JWKS metadata with automatic key rotation.
    /// </summary>
    private static ConfigurationManager<OpenIdConnectConfiguration> _defaultConfigurationManager = CreateConfigurationManager(_defaultOpenIdConnectConfigurationUrl, httpClient: null);

    /// <summary>
    /// Sets the URL used to retrieve the default OpenID Connect configuration.
    /// </summary>
    /// <param name="url">The OpenID Connect configuration URL.</param>
    internal static void SetOpenIdConnectConfigurationUrl(string url = _defaultOpenIdConnectConfigurationUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ConfigurationManager<OpenIdConnectConfiguration> manager = CreateConfigurationManager(url, httpClient: null);
        _defaultOpenIdConnectMetadataAddress = url;
        _ = Interlocked.Exchange(ref _defaultConfigurationManager, manager);
        _configurationManagers[url] = manager;
    }

    /// <summary>
    /// Gets a <see cref="ConfigurationManager{T}"/> for the specified metadata address and optional <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="metadataAddress">The OpenID Connect metadata discovery URL.</param>
    /// <param name="httpClient">Optional custom <see cref="HttpClient"/> for discovery and JWKS requests.</param>
    /// <returns>A <see cref="ConfigurationManager{T}"/> instance.</returns>
    internal static ConfigurationManager<OpenIdConnectConfiguration> GetConfigurationManager(string metadataAddress, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataAddress);

        return httpClient is not null
            ? CreateConfigurationManager(metadataAddress, httpClient)
            : _configurationManagers.GetOrAdd(metadataAddress, static address => CreateConfigurationManager(address, httpClient: null));
    }

    /// <summary>
    /// Asynchronously retrieves the <see cref="OpenIdConnectConfiguration"/> from the configured discovery endpoint.
    /// </summary>
    /// <param name="metadataAddress">Optional OpenID Connect metadata URL override.</param>
    /// <param name="httpClient">Optional custom <see cref="HttpClient"/> for discovery and JWKS requests.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The resolved <see cref="OpenIdConnectConfiguration"/>.</returns>
    internal static Task<OpenIdConnectConfiguration> GetOpenIdConnectConfigurationAsync(
        string? metadataAddress = null,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        ConfigurationManager<OpenIdConnectConfiguration> manager = string.IsNullOrWhiteSpace(metadataAddress) && httpClient is null
            ? Volatile.Read(ref _defaultConfigurationManager)
            : GetConfigurationManager(
                string.IsNullOrWhiteSpace(metadataAddress) ? _defaultOpenIdConnectMetadataAddress : metadataAddress,
                httpClient);

        return manager.GetConfigurationAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a new <see cref="ConfigurationManager{T}"/> for retrieving OpenID Connect metadata and JWKS keys.
    /// </summary>
    /// <param name="metadataAddress">The metadata discovery URL.</param>
    /// <param name="httpClient">Optional custom <see cref="HttpClient"/>.</param>
    /// <returns>A configured <see cref="ConfigurationManager{T}"/>.</returns>
    private static ConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(string metadataAddress, HttpClient? httpClient)
    {
        HttpDocumentRetriever documentRetriever = httpClient is not null
            ? new(httpClient) { RequireHttps = metadataAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase) }
            : new() { RequireHttps = metadataAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase) };

        return new(metadataAddress, new OpenIdConnectConfigurationRetriever(), documentRetriever);
    }

    /// <summary>
    /// Validates and converts <see cref="IdToken"/>, if present, to a <see cref="JsonWebToken"/>.
    /// </summary>
    /// <returns>A <see cref="JsonWebToken"/> if <see cref="IdToken"/> is a valid JWT, else <see langword="null"/>.</returns>
    public Task<JsonWebToken?> GetJwtTokenAsync() => this.GetJwtTokenAsync(null, null);

    /// <summary>
    /// Validates and converts <see cref="IdToken"/>, if present, to a <see cref="JsonWebToken"/> using the specified expected audience, optional nonce, and discovery overrides.
    /// </summary>
    /// <param name="expectedAudience">The expected JWT audience (<c>aud</c> claim). Defaults to <see cref="TwitchApi.ClientId"/> when <see langword="null"/>.</param>
    /// <param name="expectedNonce">The expected <c>nonce</c> claim value, or <see langword="null"/> or whitespace to skip nonce validation.</param>
    /// <param name="metadataAddress">Optional OpenID Connect discovery URL override.</param>
    /// <param name="httpClient">Optional custom <see cref="HttpClient"/> for discovery and JWKS requests.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="JsonWebToken"/> if <see cref="IdToken"/> is a valid JWT matching the expected audience and nonce, else <see langword="null"/>.</returns>
    public async Task<JsonWebToken?> GetJwtTokenAsync(
        string? expectedAudience,
        string? expectedNonce = null,
        string? metadataAddress = null,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(this.IdToken))
        {
            OpenIdConnectConfiguration config = await GetOpenIdConnectConfigurationAsync(metadataAddress, httpClient, cancellationToken).ConfigureAwait(false);
            TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(this.IdToken, new TokenValidationParameters
            {
                IssuerSigningKeys = config.SigningKeys,
                ValidAudience = expectedAudience ?? TwitchApi.ClientId,
                ValidIssuer = config.Issuer,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true
            }).ConfigureAwait(false);

            if (result.IsValid && result.SecurityToken is JsonWebToken jwt)
            {
                return !string.IsNullOrWhiteSpace(expectedNonce) &&
                    (!jwt.TryGetClaim("nonce", out Claim? nonceClaim) || !string.Equals(nonceClaim.Value, expectedNonce, StringComparison.Ordinal))
                    ? null
                    : jwt;
            }
        }

        return null;
    }
}
