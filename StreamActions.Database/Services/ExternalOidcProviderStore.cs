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
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Security;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="IExternalOidcProviderStore"/>.
/// </summary>
public sealed class ExternalOidcProviderStore : IExternalOidcProviderStore
{
    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The secret protection service used to encrypt and decrypt OIDC client secrets.
    /// </summary>
    private readonly SecretProtectionService _secretProtection;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalOidcProviderStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="secretProtection">The secret protection service.</param>
    public ExternalOidcProviderStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        SecretProtectionService secretProtection)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(secretProtection);

        this._dbContextFactory = dbContextFactory;
        this._secretProtection = secretProtection;
    }

    /// <inheritdoc />
    public async ValueTask<ExternalOidcProvider?> GetByIdAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.ExternalOidcProviders
                .FirstOrDefaultAsync(x => x.Id == providerId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<ExternalOidcProvider?> GetBySlugAsync(
        Guid? workspaceId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        string normalizedSlug = NormalizeSlug(slug);
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.ExternalOidcProviders
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == workspaceId && x.Slug == normalizedSlug,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ExternalOidcProvider>> GetProvidersAsync(
        Guid? workspaceId,
        bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            IQueryable<ExternalOidcProvider> query = db.ExternalOidcProviders
                .Where(x => x.WorkspaceId == workspaceId);

            if (enabledOnly)
            {
                query = query.Where(static x => x.IsEnabled);
            }

            return await query
                .OrderBy(static x => x.DisplayName)
                .ThenBy(static x => x.Slug)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<ExternalOidcProvider> UpsertAsync(
        ExternalOidcProvider provider,
        string? plaintextClientSecret = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.Slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.AuthorityUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.ClientId);

        string normalizedSlug = NormalizeSlug(provider.Slug);
        string? trimmedSecret = null;
        if (plaintextClientSecret is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(plaintextClientSecret);
            trimmedSecret = plaintextClientSecret.Trim();
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            ExternalOidcProvider? existing = provider.Id != Guid.Empty
                ? await db.ExternalOidcProviders
                    .AsTracking()
                    .FirstOrDefaultAsync(x => x.Id == provider.Id, cancellationToken)
                    .ConfigureAwait(false)
                : await db.ExternalOidcProviders
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => x.WorkspaceId == provider.WorkspaceId && x.Slug == normalizedSlug,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (existing is not null)
            {
                string? newEncryptedSecret = null;
                if (trimmedSecret is not null)
                {
                    newEncryptedSecret = this._secretProtection.ProtectExternalOidcSecret(
                        provider.WorkspaceId,
                        normalizedSlug,
                        trimmedSecret);
                }
                else if (existing.WorkspaceId != provider.WorkspaceId
                    || !string.Equals(existing.Slug, normalizedSlug, StringComparison.Ordinal))
                {
                    (bool success, string? existingSecret, CredentialStatus _) = this._secretProtection.TryUnprotectExternalOidcSecret(
                        existing.WorkspaceId,
                        existing.Slug,
                        existing.EncryptedClientSecret);

                    if (!success || string.IsNullOrWhiteSpace(existingSecret))
                    {
                        throw new InvalidOperationException(
                            "Cannot change the OIDC provider slug or workspace scope without supplying a client secret because the existing secret cannot be decrypted.");
                    }

                    newEncryptedSecret = this._secretProtection.ProtectExternalOidcSecret(
                        provider.WorkspaceId,
                        normalizedSlug,
                        existingSecret);
                }

                existing.WorkspaceId = provider.WorkspaceId;
                existing.Slug = normalizedSlug;
                existing.DisplayName = provider.DisplayName.Trim();
                existing.AuthorityUrl = provider.AuthorityUrl.Trim();
                existing.ClientId = provider.ClientId.Trim();
                existing.ScopesJson = string.IsNullOrWhiteSpace(provider.ScopesJson) ? "[]" : provider.ScopesJson.Trim();
                existing.GroupClaimType = string.IsNullOrWhiteSpace(provider.GroupClaimType) ? null : provider.GroupClaimType.Trim();
                existing.IsEnabled = provider.IsEnabled;

                if (newEncryptedSecret is not null)
                {
                    existing.EncryptedClientSecret = newEncryptedSecret;
                }

                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }

            if (string.IsNullOrWhiteSpace(trimmedSecret))
            {
                throw new ArgumentException("A plaintext OIDC client secret is required when creating a new provider.", nameof(plaintextClientSecret));
            }

            ExternalOidcProvider persisted = new()
            {
                Id = provider.Id == Guid.Empty ? Guid.CreateVersion7() : provider.Id,
                WorkspaceId = provider.WorkspaceId,
                Slug = normalizedSlug,
                DisplayName = provider.DisplayName.Trim(),
                AuthorityUrl = provider.AuthorityUrl.Trim(),
                ClientId = provider.ClientId.Trim(),
                EncryptedClientSecret = this._secretProtection.ProtectExternalOidcSecret(
                    provider.WorkspaceId,
                    normalizedSlug,
                    trimmedSecret),
                ScopesJson = string.IsNullOrWhiteSpace(provider.ScopesJson) ? "[]" : provider.ScopesJson.Trim(),
                GroupClaimType = string.IsNullOrWhiteSpace(provider.GroupClaimType) ? null : provider.GroupClaimType.Trim(),
                IsEnabled = provider.IsEnabled,
            };

            _ = db.ExternalOidcProviders.Add(persisted);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return persisted;
        }
    }

    /// <inheritdoc />
    public async ValueTask<(bool Success, string? ClientSecret, CredentialStatus Status)> TryGetClientSecretAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        ExternalOidcProvider? provider = await this.GetByIdAsync(providerId, cancellationToken).ConfigureAwait(false);
        return provider is null || string.IsNullOrWhiteSpace(provider.EncryptedClientSecret)
            ? ((bool Success, string? ClientSecret, CredentialStatus Status))(false, null, CredentialStatus.Revoked)
            : ((bool Success, string? ClientSecret, CredentialStatus Status))this._secretProtection.TryUnprotectExternalOidcSecret(
            provider.WorkspaceId,
            provider.Slug,
            provider.EncryptedClientSecret);
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            int deleted = await db.ExternalOidcProviders
                .Where(x => x.Id == providerId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return deleted > 0;
        }
    }

    /// <summary>
    /// Normalizes a provider slug to trimmed lowercase invariant form.
    /// </summary>
    /// <param name="slug">The raw slug.</param>
    /// <returns>The normalized slug.</returns>
    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();
}
