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
using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Services;

/// <summary>
/// Implements keyed single-flight OAuth token refreshing and proactive maintenance passes across all stored credentials.
/// </summary>
public sealed partial class TokenLifecycleService : ITokenLifecycleService, IDisposable
{
    /// <summary>
    /// Proactive renewal window prior to <see cref="PlatformCredential.ExpiresAtUtc"/>.
    /// </summary>
    public static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Minimum age since <see cref="PlatformCredential.UpdatedAtUtc"/> before an active token is periodically validated.
    /// </summary>
    public static readonly TimeSpan ValidationInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// Single-flight refresh semaphores keyed by <see cref="PlatformCredential.Id"/>.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _refreshLocks = new();

    /// <summary>
    /// The encrypted credential persistence store.
    /// </summary>
    private readonly IPlatformCredentialStore _credentialStore;

    /// <summary>
    /// Registered platform OAuth providers keyed by normalized platform identifier.
    /// </summary>
    private readonly Dictionary<string, IPlatformOAuthProvider> _providers;

    /// <summary>
    /// Scope factory used to resolve scoped <see cref="IOAuthClientConfigResolver"/> instances safely from this singleton.
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Time provider used for expiration and validation interval checks.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Logger for token refresh and validation diagnostics.
    /// </summary>
    private readonly ILogger<TokenLifecycleService> _logger;

    /// <summary>
    /// Indicates whether this instance has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenLifecycleService"/> class.
    /// </summary>
    /// <param name="credentialStore">The encrypted credential persistence store.</param>
    /// <param name="providers">All registered platform OAuth providers.</param>
    /// <param name="scopeFactory">The service scope factory for resolving scoped OAuth client configuration resolvers.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger instance.</param>
    public TokenLifecycleService(
        IPlatformCredentialStore credentialStore,
        IEnumerable<IPlatformOAuthProvider> providers,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<TokenLifecycleService> logger)
    {
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this._credentialStore = credentialStore;
        this._scopeFactory = scopeFactory;
        this._timeProvider = timeProvider;
        this._logger = logger;
        this._providers = new Dictionary<string, IPlatformOAuthProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (IPlatformOAuthProvider provider in providers)
        {
            if (!string.IsNullOrWhiteSpace(provider.PlatformId))
            {
                this._providers[NormalizePlatformId(provider.PlatformId)] = provider;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<TokenLifecycleResult> EnsureValidTokenAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        PlatformCredential? record = await this._credentialStore
            .GetCredentialRecordAsync(workspaceId, normalizedPlatformId, tokenRole, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return new TokenLifecycleResult(TokenLifecycleStatus.NotFound);
        }

        if (record.Status is CredentialStatus.Revoked or CredentialStatus.KeyRingMismatchRequiresReauth)
        {
            return CreateTerminalResult(record.Status);
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset refreshThreshold = now + RefreshWindow;

        if (!forceRefresh
            && record.Status == CredentialStatus.Active
            && (record.ExpiresAtUtc is null || record.ExpiresAtUtc > refreshThreshold))
        {
            (bool found, CredentialStatus status, PlatformTokenPayload? payload) = await this._credentialStore
                .TryGetTokenAsync(workspaceId, normalizedPlatformId, tokenRole, cancellationToken)
                .ConfigureAwait(false);

            if (!found)
            {
                return new TokenLifecycleResult(TokenLifecycleStatus.NotFound);
            }

            if (status is CredentialStatus.Revoked or CredentialStatus.KeyRingMismatchRequiresReauth)
            {
                return CreateTerminalResult(status);
            }

            if (status == CredentialStatus.Active && payload is not null && !string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                return new TokenLifecycleResult(TokenLifecycleStatus.Valid, payload, status, WasRefreshed: false);
            }
        }

        DateTimeOffset observedUpdatedAtUtc = record.UpdatedAtUtc;
        SemaphoreSlim gate = this._refreshLocks.GetOrAdd(record.Id, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            PlatformCredential? latestRecord = await this._credentialStore
                .GetCredentialRecordAsync(workspaceId, normalizedPlatformId, tokenRole, cancellationToken)
                .ConfigureAwait(false);

            if (latestRecord is null)
            {
                return new TokenLifecycleResult(TokenLifecycleStatus.NotFound);
            }

            if (latestRecord.Status is CredentialStatus.Revoked or CredentialStatus.KeyRingMismatchRequiresReauth)
            {
                return CreateTerminalResult(latestRecord.Status);
            }

            (bool latestFound, CredentialStatus latestStatus, PlatformTokenPayload? latestPayload) = await this._credentialStore
                .TryGetTokenAsync(workspaceId, normalizedPlatformId, tokenRole, cancellationToken)
                .ConfigureAwait(false);

            if (!latestFound)
            {
                return new TokenLifecycleResult(TokenLifecycleStatus.NotFound);
            }

            if (latestStatus is CredentialStatus.Revoked or CredentialStatus.KeyRingMismatchRequiresReauth)
            {
                return CreateTerminalResult(latestStatus);
            }

            if (latestPayload is null || string.IsNullOrWhiteSpace(latestPayload.AccessToken))
            {
                return new TokenLifecycleResult(
                    TokenLifecycleStatus.KeyRingMismatchRequiresReauth,
                    CredentialStatus: CredentialStatus.KeyRingMismatchRequiresReauth);
            }

            now = this._timeProvider.GetUtcNow();
            refreshThreshold = now + RefreshWindow;

            bool refreshedByAnotherCaller = latestRecord.UpdatedAtUtc != observedUpdatedAtUtc
                && latestRecord.Status == CredentialStatus.Active;
            bool nowWithinValidWindow = !forceRefresh
                && latestRecord.Status == CredentialStatus.Active
                && (latestRecord.ExpiresAtUtc is null || latestRecord.ExpiresAtUtc > refreshThreshold);

            return refreshedByAnotherCaller || nowWithinValidWindow
                ? new TokenLifecycleResult(TokenLifecycleStatus.Valid, latestPayload, latestRecord.Status, WasRefreshed: false)
                : await this.RefreshCredentialCoreAsync(
                    workspaceId,
                    normalizedPlatformId,
                    tokenRole,
                    latestRecord,
                    latestPayload,
                    now,
                    cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Maintenance pass isolates per-credential validation/refresh failures so remaining credentials continue to be processed.")]
    public async ValueTask RunMaintenancePassAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        IReadOnlyList<PlatformCredential> credentials = await this._credentialStore
            .GetAllCredentialsAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (PlatformCredential credential in credentials)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (credential.Status != CredentialStatus.Active)
            {
                continue;
            }

            DateTimeOffset now = this._timeProvider.GetUtcNow();
            DateTimeOffset refreshThreshold = now + RefreshWindow;

            if (credential.ExpiresAtUtc is not null && credential.ExpiresAtUtc <= refreshThreshold)
            {
                _ = await this.EnsureValidTokenAsync(
                    credential.WorkspaceId,
                    credential.PlatformId,
                    credential.TokenRole,
                    forceRefresh: false,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (now - credential.UpdatedAtUtc < ValidationInterval)
            {
                continue;
            }

            if (!this._providers.TryGetValue(NormalizePlatformId(credential.PlatformId), out IPlatformOAuthProvider? provider))
            {
                continue;
            }

            (bool found, CredentialStatus status, PlatformTokenPayload? payload) = await this._credentialStore
                .TryGetTokenAsync(credential.WorkspaceId, credential.PlatformId, credential.TokenRole, cancellationToken)
                .ConfigureAwait(false);

            if (!found || status != CredentialStatus.Active || payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                continue;
            }

            PlatformTokenValidationResult validation;
            try
            {
                validation = await provider
                    .ValidateTokenAsync(payload.AccessToken, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogValidationError(this._logger, credential.PlatformId, credential.TokenRole, credential.WorkspaceId, ex);
                continue;
            }

            if (validation.IsValid)
            {
                PlatformTokenPayload updatedPayload = payload with
                {
                    ExpiresAtUtc = validation.ExpiresAtUtc ?? payload.ExpiresAtUtc,
                    Scopes = validation.Scopes is { Count: > 0 } ? validation.Scopes : payload.Scopes,
                    PlatformUserId = string.IsNullOrWhiteSpace(validation.PlatformUserId) ? payload.PlatformUserId : validation.PlatformUserId,
                    PlatformUsername = string.IsNullOrWhiteSpace(validation.PlatformUsername) ? payload.PlatformUsername : validation.PlatformUsername,
                    ClientId = string.IsNullOrWhiteSpace(validation.ClientId) ? payload.ClientId : validation.ClientId,
                };

                _ = await this._credentialStore
                    .SaveTokenAsync(credential.WorkspaceId, credential.PlatformId, credential.TokenRole, updatedPayload, CredentialStatus.Active, cancellationToken)
                    .ConfigureAwait(false);

                if (updatedPayload.ExpiresAtUtc is not null && updatedPayload.ExpiresAtUtc <= this._timeProvider.GetUtcNow() + RefreshWindow)
                {
                    _ = await this.EnsureValidTokenAsync(
                        credential.WorkspaceId,
                        credential.PlatformId,
                        credential.TokenRole,
                        forceRefresh: true,
                        cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            TokenLifecycleResult refreshResult = await this.EnsureValidTokenAsync(
                credential.WorkspaceId,
                credential.PlatformId,
                credential.TokenRole,
                forceRefresh: true,
                cancellationToken).ConfigureAwait(false);

            if (!refreshResult.IsUsable
                && refreshResult.Status is TokenLifecycleStatus.MissingRefreshToken or TokenLifecycleStatus.RefreshFailed)
            {
                CredentialStatus failureStatus = (credential.ExpiresAtUtc is not null && credential.ExpiresAtUtc <= this._timeProvider.GetUtcNow())
                    ? CredentialStatus.Expired
                    : CredentialStatus.Revoked;

                _ = await this._credentialStore
                    .UpdateStatusAsync(credential.WorkspaceId, credential.PlatformId, credential.TokenRole, failureStatus, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        foreach (SemaphoreSlim semaphore in this._refreshLocks.Values)
        {
            semaphore.Dispose();
        }

        this._refreshLocks.Clear();
    }

    /// <summary>
    /// Executes the OAuth token refresh against the registered <see cref="IPlatformOAuthProvider"/> and persists the result.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="normalizedPlatformId">The normalized platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="record">The current credential record.</param>
    /// <param name="payload">The current decrypted token payload.</param>
    /// <param name="now">The current UTC timestamp.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resulting <see cref="TokenLifecycleResult"/>.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Refresh failures are converted into TokenLifecycleResult diagnostics instead of crashing callers or background maintenance.")]
    private async ValueTask<TokenLifecycleResult> RefreshCredentialCoreAsync(
        Guid? workspaceId,
        string normalizedPlatformId,
        TokenRole tokenRole,
        PlatformCredential record,
        PlatformTokenPayload payload,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!this._providers.TryGetValue(normalizedPlatformId, out IPlatformOAuthProvider? provider))
        {
            return new TokenLifecycleResult(
                TokenLifecycleStatus.OAuthClientNotConfigured,
                CredentialStatus: record.Status,
                ErrorMessage: $"No OAuth provider is registered for platform '{normalizedPlatformId}'.");
        }

        if (string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            CredentialStatus statusToPersist = record.Status;
            if (record.ExpiresAtUtc is not null && record.ExpiresAtUtc <= now && record.Status == CredentialStatus.Active)
            {
                _ = await this._credentialStore
                    .UpdateStatusAsync(workspaceId, normalizedPlatformId, tokenRole, CredentialStatus.Expired, cancellationToken)
                    .ConfigureAwait(false);
                statusToPersist = CredentialStatus.Expired;
            }

            return new TokenLifecycleResult(
                TokenLifecycleStatus.MissingRefreshToken,
                CredentialStatus: statusToPersist,
                ErrorMessage: "Stored credential does not contain a refresh token.");
        }

        string flow = (workspaceId, tokenRole) switch
        {
            (_, TokenRole.Broadcaster) => "broadcaster",
            (not null, TokenRole.BotChat) => "bot",
            (null, TokenRole.BotChat) => "instance-bot",
            _ => "login",
        };

        AsyncServiceScope scope = this._scopeFactory.CreateAsyncScope();
        ResolvedOAuthClientConfig? clientConfig;
        await using (scope.ConfigureAwait(false))
        {
            IOAuthClientConfigResolver configResolver = scope.ServiceProvider.GetRequiredService<IOAuthClientConfigResolver>();
            clientConfig = await configResolver
                .ResolveAsync(normalizedPlatformId, workspaceId, flow: flow, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        if (clientConfig is null)
        {
            return new TokenLifecycleResult(
                TokenLifecycleStatus.OAuthClientNotConfigured,
                CredentialStatus: record.Status,
                ErrorMessage: $"OAuth client credentials are not configured for platform '{normalizedPlatformId}'.");
        }

        PlatformOAuthTokenResult refreshed;
        try
        {
            refreshed = await provider
                .RefreshTokenAsync(clientConfig.ClientId, clientConfig.ClientSecret, payload.RefreshToken, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRefreshFailed(this._logger, normalizedPlatformId, tokenRole, workspaceId, ex);

            CredentialStatus failureStatus = record.Status;
            if (record.ExpiresAtUtc is not null && record.ExpiresAtUtc <= this._timeProvider.GetUtcNow() && record.Status == CredentialStatus.Active)
            {
                _ = await this._credentialStore
                    .UpdateStatusAsync(workspaceId, normalizedPlatformId, tokenRole, CredentialStatus.Expired, cancellationToken)
                    .ConfigureAwait(false);
                failureStatus = CredentialStatus.Expired;
            }

            return new TokenLifecycleResult(
                TokenLifecycleStatus.RefreshFailed,
                CredentialStatus: failureStatus,
                ErrorMessage: ex.Message);
        }

        PlatformTokenPayload updatedPayload = new(
            refreshed.AccessToken,
            string.IsNullOrWhiteSpace(refreshed.RefreshToken) ? payload.RefreshToken : refreshed.RefreshToken,
            refreshed.ExpiresAtUtc,
            refreshed.Scopes is { Count: > 0 } ? refreshed.Scopes : payload.Scopes,
            string.IsNullOrWhiteSpace(refreshed.TokenType) ? payload.TokenType : refreshed.TokenType,
            string.IsNullOrWhiteSpace(refreshed.PlatformUserId) ? payload.PlatformUserId : refreshed.PlatformUserId,
            string.IsNullOrWhiteSpace(refreshed.PlatformUsername) ? payload.PlatformUsername : refreshed.PlatformUsername,
            clientConfig.ClientId);

        _ = await this._credentialStore
            .SaveTokenAsync(workspaceId, normalizedPlatformId, tokenRole, updatedPayload, CredentialStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        return new TokenLifecycleResult(
            TokenLifecycleStatus.Refreshed,
            updatedPayload,
            CredentialStatus.Active,
            WasRefreshed: true);
    }

    /// <summary>
    /// Creates a terminal <see cref="TokenLifecycleResult"/> for a revoked or undecryptable credential status.
    /// </summary>
    /// <param name="status">The terminal <see cref="CredentialStatus"/>.</param>
    /// <returns>The corresponding <see cref="TokenLifecycleResult"/>.</returns>
    private static TokenLifecycleResult CreateTerminalResult(CredentialStatus status) =>
        status == CredentialStatus.Revoked
            ? new TokenLifecycleResult(TokenLifecycleStatus.Revoked, CredentialStatus: status)
            : new TokenLifecycleResult(TokenLifecycleStatus.KeyRingMismatchRequiresReauth, CredentialStatus: status);

    /// <summary>
    /// Normalizes a platform identifier to trimmed lowercase invariant form.
    /// </summary>
    /// <param name="platformId">The raw platform identifier.</param>
    /// <returns>The normalized platform identifier.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes platform identifiers to lowercase invariant.")]
    private static string NormalizePlatformId(string platformId) => platformId.Trim().ToLowerInvariant();

    /// <summary>
    /// Logs a failed OAuth token refresh attempt.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="exception">The exception thrown by the OAuth provider.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to refresh {TokenRole} token for platform {PlatformId} (workspace {WorkspaceId}).")]
    private static partial void LogRefreshFailed(ILogger logger, string platformId, TokenRole tokenRole, Guid? workspaceId, Exception exception);

    /// <summary>
    /// Logs an unexpected error while validating a token during maintenance.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="exception">The exception thrown by the OAuth provider.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to validate {TokenRole} token for platform {PlatformId} (workspace {WorkspaceId}).")]
    private static partial void LogValidationError(ILogger logger, string platformId, TokenRole tokenRole, Guid? workspaceId, Exception exception);
}
