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
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Entities;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="IPanelUserStore"/>.
/// </summary>
public sealed class PanelUserStore : IPanelUserStore
{
    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The time provider used for UTC timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PanelUserStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="timeProvider">The time provider used for UTC timestamps.</param>
    public PanelUserStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this._dbContextFactory = dbContextFactory;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PanelUsers.AnyAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HasRootUserAsync(CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PanelUsers.AnyAsync(static x => x.IsRoot, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PanelUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PanelUsers
                .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PanelUser?> GetByLoginIdentityAsync(
        string providerId,
        string providerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerUserId);

        string normalizedProviderId = NormalizeValue(providerId);
        string normalizedProviderUserId = NormalizeValue(providerUserId);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await (
                from identity in db.UserLoginIdentities
                join user in db.PanelUsers on identity.PanelUserId equals user.Id
                where identity.ProviderId == normalizedProviderId
                    && identity.ProviderUserId == normalizedProviderUserId
                select user)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PanelUser>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<PanelUser> users = await db.PanelUsers
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return [.. users.OrderBy(static x => x.CreatedAtUtc).ThenBy(static x => x.Id)];
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<UserLoginIdentity>> GetLoginIdentitiesAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.UserLoginIdentities
                .Where(x => x.PanelUserId == panelUserId)
                .OrderBy(static x => x.ProviderId)
                .ThenBy(static x => x.ProviderUserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PanelUser> CreateUserAsync(
        PanelUser user,
        UserLoginIdentity? initialIdentity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.DisplayName);

        if (initialIdentity is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(initialIdentity.ProviderId);
            ArgumentException.ThrowIfNullOrWhiteSpace(initialIdentity.ProviderUserId);
            ArgumentException.ThrowIfNullOrWhiteSpace(initialIdentity.ProviderUsername);
        }

        Guid userId = user.Id == Guid.Empty ? Guid.CreateVersion7() : user.Id;
        PanelUser persistedUser = new()
        {
            Id = userId,
            DisplayName = user.DisplayName.Trim(),
            IsRoot = user.IsRoot,
            IsGlobalAdmin = user.IsGlobalAdmin,
            MfaResetByAdminNoticePending = user.MfaResetByAdminNoticePending,
            CreatedAtUtc = user.CreatedAtUtc == default ? this._timeProvider.GetUtcNow() : user.CreatedAtUtc,
        };

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            if (initialIdentity is not null)
            {
                string normalizedProviderId = NormalizeValue(initialIdentity.ProviderId);
                string normalizedProviderUserId = NormalizeValue(initialIdentity.ProviderUserId);

                bool identityExists = await db.UserLoginIdentities
                    .AnyAsync(
                        x => x.ProviderId == normalizedProviderId && x.ProviderUserId == normalizedProviderUserId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (identityExists)
                {
                    throw new InvalidOperationException(
                        $"Login identity '{normalizedProviderId}:{normalizedProviderUserId}' is already linked to a panel user.");
                }

                _ = db.UserLoginIdentities.Add(new UserLoginIdentity
                {
                    Id = initialIdentity.Id == Guid.Empty ? Guid.CreateVersion7() : initialIdentity.Id,
                    PanelUserId = userId,
                    ProviderId = normalizedProviderId,
                    ProviderUserId = normalizedProviderUserId,
                    ProviderUsername = initialIdentity.ProviderUsername.Trim(),
                });
            }

            _ = db.PanelUsers.Add(persistedUser);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return persistedUser;
    }

    /// <inheritdoc/>
    public async ValueTask<PanelUser> UpdateUserAsync(PanelUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.DisplayName);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            PanelUser? existing = await db.PanelUsers
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken)
                .ConfigureAwait(false) ?? throw new KeyNotFoundException($"Panel user '{user.Id}' was not found.");
            existing.DisplayName = user.DisplayName.Trim();
            existing.IsRoot = user.IsRoot;
            existing.IsGlobalAdmin = user.IsGlobalAdmin;
            existing.MfaResetByAdminNoticePending = user.MfaResetByAdminNoticePending;

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<UserLoginIdentity> LinkLoginIdentityAsync(
        UserLoginIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.ProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.ProviderUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.ProviderUsername);

        if (identity.PanelUserId == Guid.Empty)
        {
            throw new ArgumentException("Panel user identifier must not be empty.", nameof(identity));
        }

        string normalizedProviderId = NormalizeValue(identity.ProviderId);
        string normalizedProviderUserId = NormalizeValue(identity.ProviderUserId);
        string trimmedUsername = identity.ProviderUsername.Trim();

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            bool userExists = await db.PanelUsers
                .AnyAsync(x => x.Id == identity.PanelUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!userExists)
            {
                throw new KeyNotFoundException($"Panel user '{identity.PanelUserId}' was not found.");
            }

            UserLoginIdentity? existing = await db.UserLoginIdentities
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.ProviderId == normalizedProviderId && x.ProviderUserId == normalizedProviderUserId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                if (existing.PanelUserId != identity.PanelUserId)
                {
                    throw new InvalidOperationException(
                        $"Login identity '{normalizedProviderId}:{normalizedProviderUserId}' is already linked to another panel user.");
                }

                existing.ProviderUsername = trimmedUsername;
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }

            UserLoginIdentity persisted = new()
            {
                Id = identity.Id == Guid.Empty ? Guid.CreateVersion7() : identity.Id,
                PanelUserId = identity.PanelUserId,
                ProviderId = normalizedProviderId,
                ProviderUserId = normalizedProviderUserId,
                ProviderUsername = trimmedUsername,
            };

            _ = db.UserLoginIdentities.Add(persisted);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return persisted;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> UnlinkLoginIdentityAsync(
        Guid panelUserId,
        string providerId,
        string providerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerUserId);

        string normalizedProviderId = NormalizeValue(providerId);
        string normalizedProviderUserId = NormalizeValue(providerUserId);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            int deleted = await db.UserLoginIdentities
                .Where(
                    x => x.PanelUserId == panelUserId
                        && x.ProviderId == normalizedProviderId
                        && x.ProviderUserId == normalizedProviderUserId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return deleted > 0;
        }
    }

    /// <summary>
    /// Trims and lowercases an identifier string using invariant culture.
    /// </summary>
    /// <param name="value">The input string.</param>
    /// <returns>The normalized string.</returns>
    private static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}
