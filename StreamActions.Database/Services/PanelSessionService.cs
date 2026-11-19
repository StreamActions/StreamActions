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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core and <see cref="HybridCache"/> implementation of <see cref="IPanelSessionService"/>.
/// </summary>
public sealed class PanelSessionService : IPanelSessionService
{
    /// <summary>
    /// Maximum allowed length for <see cref="PanelUserSession.ProviderId"/>.
    /// </summary>
    private const int _maxProviderIdLength = 128;

    /// <summary>
    /// Maximum allowed length for <see cref="PanelUserSession.RemoteIpAddress"/>.
    /// </summary>
    private const int _maxRemoteIpAddressLength = 128;

    /// <summary>
    /// Maximum allowed length for <see cref="PanelUserSession.UserAgent"/>.
    /// </summary>
    private const int _maxUserAgentLength = 512;

    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The hybrid cache used for hot-path session validation.
    /// </summary>
    private readonly HybridCache _cache;

    /// <summary>
    /// The time provider used for UTC timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The optional in-process event bus used to broadcast session revocation notifications.
    /// </summary>
    private readonly IEventBus? _eventBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="PanelSessionService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="cache">The hybrid cache instance.</param>
    /// <param name="timeProvider">The optional time provider (defaults to <see cref="TimeProvider.System"/>).</param>
    /// <param name="eventBus">The optional event bus for publishing <see cref="UserSessionsRevokedEvent"/>.</param>
    public PanelSessionService(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        HybridCache cache,
        TimeProvider? timeProvider = null,
        IEventBus? eventBus = null)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(cache);

        this._dbContextFactory = dbContextFactory;
        this._cache = cache;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._eventBus = eventBus;
    }

    /// <summary>
    /// Builds the <see cref="HybridCache"/> key and tag for a specific session identifier.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The formatted session cache key/tag.</returns>
    public static string GetSessionCacheKey(Guid sessionId) => $"session:{sessionId:N}";

    /// <summary>
    /// Builds the <see cref="HybridCache"/> tag for all sessions belonging to a panel user.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <returns>The formatted user sessions cache tag.</returns>
    public static string GetUserSessionsCacheTag(Guid panelUserId) => $"user-sessions:{panelUserId:N}";

    /// <inheritdoc/>
    public async ValueTask<PanelUserSession> CreateSessionAsync(
        Guid panelUserId,
        string providerId,
        string? remoteIpAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            throw new ArgumentException("Panel user identifier cannot be empty.", nameof(panelUserId));
        }

        string normalizedProviderId = string.IsNullOrWhiteSpace(providerId)
            ? "unknown"
            : Truncate(providerId.Trim(), _maxProviderIdLength)!;
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        PanelUserSession session = new()
        {
            Id = Guid.CreateVersion7(),
            PanelUserId = panelUserId,
            ProviderId = normalizedProviderId,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            RemoteIpAddress = Truncate(remoteIpAddress?.Trim(), _maxRemoteIpAddressLength),
            UserAgent = Truncate(userAgent?.Trim(), _maxUserAgentLength),
            RevokedAtUtc = null,
        };

        StreamActionsDbContext dbContext = await this._dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            _ = dbContext.PanelUserSessions.Add(session);
            _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        string sessionKey = GetSessionCacheKey(session.Id);
        string userTag = GetUserSessionsCacheTag(session.PanelUserId);
        await this._cache.SetAsync(
            sessionKey,
            new CachedSessionValidation(IsActive: true, session.PanelUserId),
            tags: [sessionKey, userTag],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return session;
    }

    /// <inheritdoc/>
    public async ValueTask<PanelUserSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return null;
        }

        StreamActionsDbContext dbContext = await this._dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            return await dbContext.PanelUserSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PanelUserSession>> GetActiveSessionsForUserAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            return [];
        }

        StreamActionsDbContext dbContext = await this._dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            List<PanelUserSession> sessions = await dbContext.PanelUserSessions
                .AsNoTracking()
                .Where(x => x.PanelUserId == panelUserId && x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. sessions.OrderByDescending(static x => x.CreatedAtUtc)];
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsSessionValidAsync(
        Guid sessionId,
        Guid? expectedPanelUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return false;
        }

        string sessionKey = GetSessionCacheKey(sessionId);
        List<string> tags = [sessionKey];
        if (expectedPanelUserId.HasValue && expectedPanelUserId.Value != Guid.Empty)
        {
            tags.Add(GetUserSessionsCacheTag(expectedPanelUserId.Value));
        }

        CachedSessionValidation cached = await this._cache.GetOrCreateAsync(
            sessionKey,
            async ct =>
            {
                StreamActionsDbContext dbContext = await this._dbContextFactory
                    .CreateDbContextAsync(ct)
                    .ConfigureAwait(false);
                await using (dbContext.ConfigureAwait(false))
                {
                    PanelUserSession? session = await dbContext.PanelUserSessions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == sessionId, ct)
                        .ConfigureAwait(false);

                    return session is not null && session.RevokedAtUtc is null
                        ? new CachedSessionValidation(IsActive: true, session.PanelUserId)
                        : new CachedSessionValidation(IsActive: false, session?.PanelUserId ?? Guid.Empty);
                }
            },
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return cached.IsActive
            && (!expectedPanelUserId.HasValue
                || expectedPanelUserId.Value == Guid.Empty
                || cached.PanelUserId == expectedPanelUserId.Value);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return false;
        }

        Guid panelUserId;
        bool newlyRevoked = false;
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        StreamActionsDbContext dbContext = await this._dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            PanelUserSession? session = await dbContext.PanelUserSessions
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
                .ConfigureAwait(false);

            if (session is null)
            {
                await this._cache.RemoveAsync(GetSessionCacheKey(sessionId), cancellationToken).ConfigureAwait(false);
                await this._cache.RemoveByTagAsync(GetSessionCacheKey(sessionId), cancellationToken).ConfigureAwait(false);
                return false;
            }

            panelUserId = session.PanelUserId;
            if (session.RevokedAtUtc is null)
            {
                session.RevokedAtUtc = now;
                _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newlyRevoked = true;
            }
        }

        string sessionKey = GetSessionCacheKey(sessionId);
        string userTag = GetUserSessionsCacheTag(panelUserId);
        await this._cache.RemoveAsync(sessionKey, cancellationToken).ConfigureAwait(false);
        await this._cache.RemoveByTagAsync(sessionKey, cancellationToken).ConfigureAwait(false);
        await this._cache.RemoveByTagAsync(userTag, cancellationToken).ConfigureAwait(false);

        if (newlyRevoked && this._eventBus is not null)
        {
            await this._eventBus
                .PublishAsync(new UserSessionsRevokedEvent(panelUserId, ExcludedSessionId: null), cancellationToken)
                .ConfigureAwait(false);
        }

        return newlyRevoked;
    }

    /// <inheritdoc/>
    public async ValueTask<int> RevokeOtherSessionsAsync(
        Guid currentUserId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return 0;
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        List<Guid> revokedIds = [];

        StreamActionsDbContext dbContext = await this._dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            List<PanelUserSession> activeOtherSessions = await dbContext.PanelUserSessions
                .AsTracking()
                .Where(x => x.PanelUserId == currentUserId && x.RevokedAtUtc == null && x.Id != currentSessionId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (PanelUserSession session in activeOtherSessions)
            {
                session.RevokedAtUtc = now;
                revokedIds.Add(session.Id);
            }

            if (revokedIds.Count > 0)
            {
                _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (Guid id in revokedIds)
        {
            string sessionKey = GetSessionCacheKey(id);
            await this._cache.RemoveAsync(sessionKey, cancellationToken).ConfigureAwait(false);
            await this._cache.RemoveByTagAsync(sessionKey, cancellationToken).ConfigureAwait(false);
        }

        await this._cache
            .RemoveByTagAsync(GetUserSessionsCacheTag(currentUserId), cancellationToken)
            .ConfigureAwait(false);

        if (this._eventBus is not null)
        {
            await this._eventBus
                .PublishAsync(new UserSessionsRevokedEvent(currentUserId, ExcludedSessionId: currentSessionId), cancellationToken)
                .ConfigureAwait(false);
        }

        return revokedIds.Count;
    }

    /// <inheritdoc/>
    public async ValueTask<int> RevokeAllSessionsForUserAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
        {
            return 0;
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        List<Guid> revokedIds = [];

        StreamActionsDbContext dbContext = await this._dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            List<PanelUserSession> activeSessions = await dbContext.PanelUserSessions
                .AsTracking()
                .Where(x => x.PanelUserId == targetUserId && x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (PanelUserSession session in activeSessions)
            {
                session.RevokedAtUtc = now;
                revokedIds.Add(session.Id);
            }

            if (revokedIds.Count > 0)
            {
                _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (Guid id in revokedIds)
        {
            string sessionKey = GetSessionCacheKey(id);
            await this._cache.RemoveAsync(sessionKey, cancellationToken).ConfigureAwait(false);
            await this._cache.RemoveByTagAsync(sessionKey, cancellationToken).ConfigureAwait(false);
        }

        await this._cache
            .RemoveByTagAsync(GetUserSessionsCacheTag(targetUserId), cancellationToken)
            .ConfigureAwait(false);

        if (this._eventBus is not null)
        {
            await this._eventBus
                .PublishAsync(new UserSessionsRevokedEvent(targetUserId, ExcludedSessionId: null), cancellationToken)
                .ConfigureAwait(false);
        }

        return revokedIds.Count;
    }

    /// <summary>
    /// Truncates a string to at most <paramref name="maxLength"/> characters.
    /// </summary>
    /// <param name="value">The input string.</param>
    /// <param name="maxLength">The maximum allowed character length.</param>
    /// <returns>The truncated string, or <see langword="null"/> if <paramref name="value"/> is null or whitespace.</returns>
    private static string? Truncate(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ? null : value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>
    /// Represents a cached session validation result stored in <see cref="HybridCache"/>.
    /// </summary>
    /// <param name="IsActive"><see langword="true"/> if the session exists and is not revoked; otherwise, <see langword="false"/>.</param>
    /// <param name="PanelUserId">The owning panel user identifier.</param>
    private sealed record CachedSessionValidation(bool IsActive, Guid PanelUserId);
}

