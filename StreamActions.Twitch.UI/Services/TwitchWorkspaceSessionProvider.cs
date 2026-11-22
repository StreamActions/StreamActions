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
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.UI.Abstractions;
using System.Collections.ObjectModel;

namespace StreamActions.Twitch.UI.Services;

/// <summary>
/// Default <see cref="ITwitchWorkspaceSessionProvider"/> resolving stored Twitch credentials, workspace channel metadata, and OAuth client configuration.
/// </summary>
public sealed class TwitchWorkspaceSessionProvider : ITwitchWorkspaceSessionProvider
{
    #region Public Constants

    /// <summary>
    /// Canonical lowercase platform identifier for Twitch.
    /// </summary>
    public const string TwitchPlatformId = "twitch";

    #endregion Public Constants

    #region Private Fields

    /// <summary>
    /// The platform credential store used to retrieve encrypted Twitch OAuth tokens.
    /// </summary>
    private readonly IPlatformCredentialStore _credentialStore;

    /// <summary>
    /// The OAuth client config resolver used to retrieve client ID and secret values.
    /// </summary>
    private readonly IOAuthClientConfigResolver _oauthClientConfigResolver;

    /// <summary>
    /// The workspace store used to resolve attached Twitch channel identifiers.
    /// </summary>
    private readonly IWorkspaceStore _workspaceStore;

    /// <summary>
    /// The time provider passed to created <see cref="TwitchSession"/> instances.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Optional custom Helix API base URI override (primarily used in offline Kestrel tests).
    /// </summary>
    private readonly Uri? _customBaseUri;

    /// <summary>
    /// Optional custom <see cref="HttpMessageInvoker"/> override (primarily used in tests).
    /// </summary>
    private readonly HttpMessageInvoker? _customHttpClient;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TwitchWorkspaceSessionProvider"/> class.
    /// </summary>
    /// <param name="credentialStore">The encrypted platform credential store.</param>
    /// <param name="oauthClientConfigResolver">The OAuth application credential resolver.</param>
    /// <param name="workspaceStore">The workspace metadata store.</param>
    /// <param name="timeProvider">Optional <see cref="TimeProvider"/> override.</param>
    [ActivatorUtilitiesConstructor]
    public TwitchWorkspaceSessionProvider(
        IPlatformCredentialStore credentialStore,
        IOAuthClientConfigResolver oauthClientConfigResolver,
        IWorkspaceStore workspaceStore,
        TimeProvider? timeProvider = null)
        : this(credentialStore, oauthClientConfigResolver, workspaceStore, timeProvider, customBaseUri: null, customHttpClient: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TwitchWorkspaceSessionProvider"/> class with optional HTTP transport overrides.
    /// </summary>
    /// <param name="credentialStore">The encrypted platform credential store.</param>
    /// <param name="oauthClientConfigResolver">The OAuth application credential resolver.</param>
    /// <param name="workspaceStore">The workspace metadata store.</param>
    /// <param name="timeProvider">Optional <see cref="TimeProvider"/> override.</param>
    /// <param name="customBaseUri">Optional custom Twitch Helix base URI (must end with a trailing slash).</param>
    /// <param name="customHttpClient">Optional custom <see cref="HttpMessageInvoker"/> override.</param>
    public TwitchWorkspaceSessionProvider(
        IPlatformCredentialStore credentialStore,
        IOAuthClientConfigResolver oauthClientConfigResolver,
        IWorkspaceStore workspaceStore,
        TimeProvider? timeProvider,
        Uri? customBaseUri,
        HttpMessageInvoker? customHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(oauthClientConfigResolver);
        ArgumentNullException.ThrowIfNull(workspaceStore);

        this._credentialStore = credentialStore;
        this._oauthClientConfigResolver = oauthClientConfigResolver;
        this._workspaceStore = workspaceStore;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._customBaseUri = customBaseUri;
        this._customHttpClient = customHttpClient;
    }

    #endregion Public Constructors

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<TwitchWorkspaceSessionLease?> GetSessionAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            return null;
        }

        (TokenRole ResolvedRole, PlatformTokenPayload Payload)? tokenEntry = await this
            .ResolveTokenPayloadAsync(workspaceId, cancellationToken)
            .ConfigureAwait(false);
        if (tokenEntry is null)
        {
            return null;
        }

        TokenRole resolvedRole = tokenEntry.Value.ResolvedRole;
        PlatformTokenPayload payload = tokenEntry.Value.Payload;

        WorkspacePlatform? platform = await this._workspaceStore
            .GetPlatformAsync(workspaceId, TwitchPlatformId, cancellationToken)
            .ConfigureAwait(false);

        string broadcasterId = ResolveBroadcasterId(platform, resolvedRole, payload);
        string broadcasterLogin = ResolveBroadcasterLogin(platform, resolvedRole, payload);

        string flow = resolvedRole == TokenRole.Broadcaster ? "broadcaster" : "bot";
        ResolvedOAuthClientConfig? oauthConfig = await this._oauthClientConfigResolver
            .ResolveAsync(TwitchPlatformId, workspaceId, actor: null, flow: flow, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string? clientId = !string.IsNullOrWhiteSpace(oauthConfig?.ClientId)
            ? oauthConfig.ClientId.Trim()
            : !string.IsNullOrWhiteSpace(payload.ClientId)
                ? payload.ClientId.Trim()
                : null;

        string? clientSecret = !string.IsNullOrWhiteSpace(oauthConfig?.ClientSecret)
            ? oauthConfig.ClientSecret
            : null;

        TwitchToken twitchToken = new()
        {
            OAuth = payload.AccessToken,
            Refresh = payload.RefreshToken,
            Expires = payload.ExpiresAtUtc?.UtcDateTime,
            Login = payload.PlatformUsername,
            UserId = payload.PlatformUserId,
            Scopes = MapScopes(payload.Scopes),
            Type = resolvedRole == TokenRole.App
                ? TwitchToken.TokenType.App
                : TwitchToken.TokenType.User,
        };

        TwitchSession session = new()
        {
            CustomBaseUri = this._customBaseUri,
            CustomClientId = clientId,
            CustomClientSecret = clientSecret,
            CustomHttpClient = this._customHttpClient,
            TimeProvider = this._timeProvider,
            Token = twitchToken,
        };

        return new TwitchWorkspaceSessionLease(session, broadcasterId, broadcasterLogin, ownsSession: true);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Attempts to resolve an active Twitch token payload for the workspace, preferring broadcaster credentials and falling back to bot or app credentials.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved <see cref="TokenRole"/> and <see cref="PlatformTokenPayload"/>, or <see langword="null"/> if none is active.</returns>
    private async ValueTask<(TokenRole ResolvedRole, PlatformTokenPayload Payload)?> ResolveTokenPayloadAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        (Guid? ScopeWorkspaceId, TokenRole Role)[] candidates =
        [
            (workspaceId, TokenRole.Broadcaster),
            (workspaceId, TokenRole.BotChat),
            (null, TokenRole.BotChat),
            (workspaceId, TokenRole.App),
            (null, TokenRole.App),
        ];

        foreach ((Guid? scopeWorkspaceId, TokenRole role) in candidates)
        {
            (bool found, CredentialStatus status, PlatformTokenPayload? payload) = await this._credentialStore
                .TryGetTokenAsync(scopeWorkspaceId, TwitchPlatformId, role, cancellationToken)
                .ConfigureAwait(false);

            if (found
                && status == CredentialStatus.Active
                && payload is not null
                && !string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                return (role, payload);
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the workspace broadcaster user ID from <paramref name="platform"/> metadata or the broadcaster token payload.
    /// </summary>
    /// <param name="platform">The optional workspace platform attachment.</param>
    /// <param name="resolvedRole">The resolved token role.</param>
    /// <param name="payload">The decrypted token payload.</param>
    /// <returns>The resolved broadcaster ID, or empty string if unavailable.</returns>
    private static string ResolveBroadcasterId(
        WorkspacePlatform? platform,
        TokenRole resolvedRole,
        PlatformTokenPayload payload) => !string.IsNullOrWhiteSpace(platform?.PlatformChannelId)
            ? platform.PlatformChannelId.Trim()
            : resolvedRole == TokenRole.Broadcaster && !string.IsNullOrWhiteSpace(payload.PlatformUserId)
            ? payload.PlatformUserId.Trim()
            : string.Empty;

    /// <summary>
    /// Resolves the workspace broadcaster login name from <paramref name="platform"/> metadata or the broadcaster token payload.
    /// </summary>
    /// <param name="platform">The optional workspace platform attachment.</param>
    /// <param name="resolvedRole">The resolved token role.</param>
    /// <param name="payload">The decrypted token payload.</param>
    /// <returns>The resolved broadcaster login name, or empty string if unavailable.</returns>
    private static string ResolveBroadcasterLogin(
        WorkspacePlatform? platform,
        TokenRole resolvedRole,
        PlatformTokenPayload payload) => platform?.Metadata.ExtraSettings.TryGetValue("broadcaster_login", out string? extraLogin) == true
            && !string.IsNullOrWhiteSpace(extraLogin)
            ? extraLogin.Trim()
            : resolvedRole == TokenRole.Broadcaster && !string.IsNullOrWhiteSpace(payload.PlatformUsername)
            ? payload.PlatformUsername.Trim()
            : string.Empty;

    /// <summary>
    /// Maps raw OAuth scope names to strongly-typed Twitch <see cref="Scope"/> instances.
    /// </summary>
    /// <param name="rawScopes">The optional list of OAuth scope strings.</param>
    /// <returns>A read-only list of resolved <see cref="Scope"/> objects, or <see langword="null"/> when <paramref name="rawScopes"/> is <see langword="null"/>.</returns>
    private static ReadOnlyCollection<Scope>? MapScopes(IReadOnlyList<string>? rawScopes)
    {
        if (rawScopes is null)
        {
            return null;
        }

        IReadOnlyDictionary<string, Scope> knownScopes = Scope.Scopes;
        List<Scope> mapped = [];
        foreach (string rawScope in rawScopes)
        {
            if (string.IsNullOrWhiteSpace(rawScope))
            {
                continue;
            }

            if (knownScopes.TryGetValue(rawScope.Trim(), out Scope? scope))
            {
                mapped.Add(scope);
            }
        }

        return mapped.AsReadOnly();
    }

    #endregion Private Methods
}
