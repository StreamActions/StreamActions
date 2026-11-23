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

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Common.Net;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using System.Security.Claims;
using TwitchScope = StreamActions.Twitch.OAuth.Scope;

namespace StreamActions.Daemon.Platforms;

/// <summary>
/// Implements <see cref="IPlatformOAuthProvider"/> for Twitch using <see cref="TwitchOAuthClient"/> and <see cref="TwitchSession"/>.
/// </summary>
internal sealed class TwitchPlatformOAuthProvider : IPlatformOAuthProvider
{
    /// <summary>
    /// Named HTTP client identifier used with <see cref="IHttpClientFactory"/>.
    /// </summary>
    public const string HttpClientName = "TwitchOAuth";

    /// <summary>
    /// Default scopes requested for Twitch panel login and identity linking flows.
    /// </summary>
    private static readonly string[] _defaultLoginScopes = ["openid", "user:read:email"];

    /// <summary>
    /// Default scopes requested for Twitch broadcaster workspace authorization flows.
    /// </summary>
    private static readonly string[] _defaultBroadcasterScopes =
    [
        "openid",
        "user:read:email",
        "channel:bot",
        "chat:read",
        "chat:edit",
        "moderator:manage:banned_users",
        "moderator:manage:chat_messages",
    ];

    /// <summary>
    /// Default scopes requested for Twitch bot chat account authorization flows.
    /// </summary>
    private static readonly string[] _defaultBotScopes =
    [
        "user:read:chat",
        "user:write:chat",
        "user:bot",
        "chat:read",
        "chat:edit",
    ];

    /// <summary>
    /// Optional HTTP client factory for creating outbound HTTP clients.
    /// </summary>
    private readonly IHttpClientFactory? _httpClientFactory;

    /// <summary>
    /// Optional explicit HTTP client used in unit or integration tests.
    /// </summary>
    private readonly HttpClient? _explicitHttpClient;

    /// <summary>
    /// Configured Twitch OAuth/OIDC endpoint URLs.
    /// </summary>
    private readonly TwitchOAuthEndpointOptions _endpoints;

    /// <summary>
    /// Time provider used to compute token expiration timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwitchPlatformOAuthProvider"/> class for dependency injection.
    /// </summary>
    /// <param name="httpClientFactory">Optional HTTP client factory.</param>
    /// <param name="endpointOptions">Optional endpoint URL overrides.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    [ActivatorUtilitiesConstructor]
    public TwitchPlatformOAuthProvider(
        IHttpClientFactory? httpClientFactory = null,
        IOptions<TwitchOAuthEndpointOptions>? endpointOptions = null,
        TimeProvider? timeProvider = null)
    {
        this._httpClientFactory = httpClientFactory;
        this._endpoints = endpointOptions?.Value ?? new TwitchOAuthEndpointOptions();
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TwitchPlatformOAuthProvider"/> class with explicit test dependencies.
    /// </summary>
    /// <param name="endpointOptions">Endpoint URL overrides.</param>
    /// <param name="httpClient">Optional explicit HTTP client.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    public TwitchPlatformOAuthProvider(
        TwitchOAuthEndpointOptions endpointOptions,
        HttpClient? httpClient = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(endpointOptions);

        this._endpoints = endpointOptions;
        this._explicitHttpClient = httpClient;
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string PlatformId => "twitch";

    /// <inheritdoc />
    public IReadOnlyList<string> DefaultLoginScopes => _defaultLoginScopes;

    /// <inheritdoc />
    public IReadOnlyList<string> DefaultBroadcasterScopes => _defaultBroadcasterScopes;

    /// <inheritdoc />
    public IReadOnlyList<string> DefaultBotScopes => _defaultBotScopes;

    /// <inheritdoc />
    public Uri BuildAuthorizationUri(
        string clientId,
        Uri redirectUri,
        IReadOnlyCollection<string>? scopes,
        string state,
        bool forceVerify = false,
        string? nonce = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        return !string.IsNullOrWhiteSpace(nonce)
            ? TwitchOAuthClient.BuildOidcAuthorizationUri(
                clientId,
                redirectUri,
                scopes,
                state,
                nonce,
                forceVerify,
                claimsJson: null,
                authorizeEndpoint: this._endpoints.AuthorizeEndpoint)
            : TwitchOAuthClient.BuildAuthorizationUri(
                clientId,
                redirectUri,
                scopes,
                state,
                forceVerify,
                authorizeEndpoint: this._endpoints.AuthorizeEndpoint);
    }

    /// <inheritdoc />
    public async ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(
        string clientId,
        string clientSecret,
        string code,
        Uri redirectUri,
        string? expectedNonce = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(redirectUri);

        (HttpClient? httpClient, bool shouldDisposeHttp) = this.AcquireHttpClient();
        try
        {
            using TwitchSession exchangeSession = new()
            {
                CancellationToken = cancellationToken,
                CustomClientId = clientId,
                CustomClientSecret = clientSecret,
                CustomHttpClient = httpClient,
                TimeProvider = this._timeProvider,
                Token = TwitchToken.Empty,
            };

            Token? token = await TwitchOAuthClient
                .ExchangeAuthorizationCodeAsync(code, redirectUri, exchangeSession, this._endpoints.TokenEndpoint)
                .ConfigureAwait(false);

            if (token is null || !token.IsSuccessStatusCode || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException(
                    $"Twitch authorization code exchange failed: {token?.Message ?? token?.Status.ToString() ?? "no response"}".Trim());
            }

            IReadOnlyList<string> scopes = MapScopes(token.Scopes);
            DateTimeOffset? expiresAtUtc = token.ExpiresIn > 0
                ? this._timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn)
                : null;

            string? platformUserId = null;
            string? platformUsername = null;
            string? displayName = null;
            string? email = null;

            if (!string.IsNullOrWhiteSpace(token.IdToken))
            {
                JsonWebToken jwt = await this.ValidateIdTokenAsync(
                    token.IdToken,
                    clientId,
                    expectedNonce,
                    httpClient,
                    cancellationToken).ConfigureAwait(false);

                platformUserId = GetClaimValue(jwt, "sub") ?? jwt.Subject;
                platformUsername = GetClaimValue(jwt, "preferred_username");
                email = GetClaimValue(jwt, "email");
                displayName = platformUsername;

                if (string.IsNullOrWhiteSpace(platformUsername)
                    && scopes.Contains("openid", StringComparer.Ordinal))
                {
                    using TwitchSession userInfoSession = new()
                    {
                        CancellationToken = cancellationToken,
                        CustomClientId = clientId,
                        CustomHttpClient = httpClient,
                        TimeProvider = this._timeProvider,
                        Token = TwitchToken.FromApiResponse(token, TwitchToken.TokenType.User, platformUserId, platformUsername),
                    };

                    UserInfo? userInfo = await TwitchOAuthClient
                        .GetUserInfoAsync(userInfoSession, this._endpoints.UserInfoEndpoint)
                        .ConfigureAwait(false);

                    if (userInfo is not null && userInfo.IsSuccessStatusCode)
                    {
                        platformUserId ??= userInfo.Subject;
                        platformUsername ??= userInfo.PreferredUsername;
                        email ??= userInfo.Email;
                        displayName ??= userInfo.PreferredUsername;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(platformUserId) || string.IsNullOrWhiteSpace(platformUsername))
            {
                PlatformTokenValidationResult validated = await this.ValidateTokenInternalAsync(
                    token.AccessToken,
                    clientId,
                    httpClient,
                    cancellationToken).ConfigureAwait(false);

                if (validated.IsValid)
                {
                    platformUserId ??= validated.PlatformUserId;
                    platformUsername ??= validated.PlatformUsername;
                    displayName ??= validated.PlatformUsername;
                }
            }

            return new PlatformOAuthTokenResult(
                token.AccessToken,
                token.RefreshToken,
                expiresAtUtc,
                scopes,
                token.TokenType,
                token.IdToken,
                platformUserId,
                platformUsername,
                displayName,
                email);
        }
        finally
        {
            if (shouldDisposeHttp)
            {
                httpClient?.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(
        string clientId,
        string clientSecret,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        (HttpClient? httpClient, bool shouldDisposeHttp) = this.AcquireHttpClient();
        try
        {
            using TwitchSession session = new()
            {
                CancellationToken = cancellationToken,
                CustomClientId = clientId,
                CustomClientSecret = clientSecret,
                CustomHttpClient = httpClient,
                TimeProvider = this._timeProvider,
                Token = new TwitchToken
                {
                    OAuth = "refresh-placeholder",
                    Refresh = refreshToken,
                    Type = TwitchToken.TokenType.User,
                },
            };

            Token? refreshed = await TwitchOAuthClient
                .RefreshTokenAsync(session, this._endpoints.TokenEndpoint)
                .ConfigureAwait(false);

            if (refreshed is null || !refreshed.IsSuccessStatusCode || string.IsNullOrWhiteSpace(refreshed.AccessToken))
            {
                throw new InvalidOperationException(
                    $"Twitch token refresh failed: {refreshed?.Message ?? refreshed?.Status.ToString() ?? "no response"}".Trim());
            }

            DateTimeOffset? expiresAtUtc = refreshed.ExpiresIn > 0
                ? this._timeProvider.GetUtcNow().AddSeconds(refreshed.ExpiresIn)
                : null;

            return new PlatformOAuthTokenResult(
                refreshed.AccessToken,
                refreshed.RefreshToken ?? refreshToken,
                expiresAtUtc,
                MapScopes(refreshed.Scopes),
                refreshed.TokenType,
                refreshed.IdToken);
        }
        finally
        {
            if (shouldDisposeHttp)
            {
                httpClient?.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        (HttpClient? httpClient, bool shouldDisposeHttp) = this.AcquireHttpClient();
        try
        {
            return await this.ValidateTokenInternalAsync(
                accessToken,
                TwitchApi.ClientId ?? "oauth-validate",
                httpClient,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (shouldDisposeHttp)
            {
                httpClient?.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask RevokeTokenAsync(
        string clientId,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        (HttpClient? httpClient, bool shouldDisposeHttp) = this.AcquireHttpClient();
        try
        {
            using TwitchSession session = new()
            {
                CancellationToken = cancellationToken,
                CustomClientId = clientId,
                CustomHttpClient = httpClient,
                TimeProvider = this._timeProvider,
                Token = new TwitchToken
                {
                    OAuth = token,
                    Type = TwitchToken.TokenType.User,
                },
            };

            JsonApiResponse? response = await TwitchOAuthClient
                .RevokeTokenAsync(session, this._endpoints.RevokeEndpoint)
                .ConfigureAwait(false);

            if (response is not null && !response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Twitch token revocation failed with status {response.Status}: {response.Message}".Trim());
            }
        }
        finally
        {
            if (shouldDisposeHttp)
            {
                httpClient?.Dispose();
            }
        }
    }

    /// <summary>
    /// Converts Twitch <see cref="TwitchScope"/> instances to normalized scope name strings.
    /// </summary>
    /// <param name="scopes">The Twitch scope list.</param>
    /// <returns>A list of scope strings.</returns>
    private static List<string> MapScopes(IReadOnlyList<TwitchScope>? scopes) =>
        scopes is null
            ? []
            : [.. scopes.Where(static s => !string.IsNullOrWhiteSpace(s.Name)).Select(static s => s.Name!)];

    /// <summary>
    /// Extracts a non-whitespace claim value from a validated JWT.
    /// </summary>
    /// <param name="jwt">The validated JWT.</param>
    /// <param name="claimType">The claim type name.</param>
    /// <returns>The trimmed claim value, or <see langword="null"/> if absent.</returns>
    private static string? GetClaimValue(JsonWebToken jwt, string claimType) =>
        jwt.TryGetClaim(claimType, out Claim? claim) && !string.IsNullOrWhiteSpace(claim.Value)
            ? claim.Value.Trim()
            : null;

    /// <summary>
    /// Validates an access token against the configured Twitch validation endpoint using the provided client identifier.
    /// </summary>
    /// <param name="accessToken">The access token to validate.</param>
    /// <param name="clientId">The client identifier header value required by <see cref="TwitchApi"/>.</param>
    /// <param name="httpClient">Optional custom HTTP client.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validation result.</returns>
    private async Task<PlatformTokenValidationResult> ValidateTokenInternalAsync(
        string accessToken,
        string clientId,
        HttpClient? httpClient,
        CancellationToken cancellationToken)
    {
        using TwitchSession session = new()
        {
            CancellationToken = cancellationToken,
            CustomClientId = clientId,
            CustomHttpClient = httpClient,
            TimeProvider = this._timeProvider,
            Token = new TwitchToken
            {
                OAuth = accessToken,
                Type = TwitchToken.TokenType.User,
            },
        };

        Validate? validate = await TwitchOAuthClient
            .ValidateTokenAsync(session, this._endpoints.ValidateEndpoint, dontRefresh: true)
            .ConfigureAwait(false);

        if (validate is null || !validate.IsSuccessStatusCode)
        {
            return new PlatformTokenValidationResult(IsValid: false);
        }

        DateTimeOffset? expiresAtUtc = validate.ExpiresIn > 0
            ? this._timeProvider.GetUtcNow().AddSeconds(validate.ExpiresIn)
            : null;

        return new PlatformTokenValidationResult(
            IsValid: true,
            ClientId: validate.ClientId,
            PlatformUserId: validate.UserId,
            PlatformUsername: validate.Login,
            Scopes: validate.Scopes ?? [],
            ExpiresAtUtc: expiresAtUtc);
    }

    /// <summary>
    /// Validates a Twitch OIDC ID token against the configured OpenID Connect discovery and JWKS endpoints.
    /// </summary>
    /// <param name="idToken">The raw JWT ID token string.</param>
    /// <param name="expectedAudience">The expected OAuth client identifier audience.</param>
    /// <param name="expectedNonce">The optional expected nonce value.</param>
    /// <param name="httpClient">Optional custom HTTP client for retrieving discovery and JWKS documents.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validated <see cref="JsonWebToken"/>.</returns>
    private async Task<JsonWebToken> ValidateIdTokenAsync(
        string idToken,
        string expectedAudience,
        string? expectedNonce,
        HttpClient? httpClient,
        CancellationToken cancellationToken)
    {
        JsonWebToken? jwt = await TwitchOAuthClient.ValidateIdTokenAsync(
            new Token { IdToken = idToken },
            expectedAudience,
            expectedNonce,
            this._endpoints.OpenIdConfigurationEndpoint,
            httpClient,
            cancellationToken).ConfigureAwait(false);

        return jwt ?? throw new SecurityTokenException("Twitch OIDC ID token validation failed.");
    }

    /// <summary>
    /// Acquires an <see cref="HttpClient"/> from the explicit instance or <see cref="IHttpClientFactory"/> when configured.
    /// </summary>
    /// <returns>The optional <see cref="HttpClient"/> and a boolean indicating whether the caller must dispose it.</returns>
    private (HttpClient? Client, bool ShouldDispose) AcquireHttpClient() =>
        this._explicitHttpClient is not null
            ? (this._explicitHttpClient, false)
            : this._httpClientFactory is not null
                ? (this._httpClientFactory.CreateClient(HttpClientName), true)
                : (null, false);
}
