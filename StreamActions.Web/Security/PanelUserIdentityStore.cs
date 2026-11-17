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

using Microsoft.AspNetCore.Identity;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;

namespace StreamActions.Web.Security;

/// <summary>
/// ASP.NET Core Identity user and passkey store backed by <see cref="IPanelUserStore"/> and <see cref="IMfaCredentialStore"/>.
/// </summary>
public sealed class PanelUserIdentityStore : IUserStore<PanelUser>, IUserPasskeyStore<PanelUser>
{
    /// <summary>
    /// The panel user persistence store.
    /// </summary>
    private readonly IPanelUserStore _panelUserStore;

    /// <summary>
    /// The MFA credential persistence store.
    /// </summary>
    private readonly IMfaCredentialStore _mfaCredentialStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="PanelUserIdentityStore"/> class.
    /// </summary>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    public PanelUserIdentityStore(
        IPanelUserStore panelUserStore,
        IMfaCredentialStore mfaCredentialStore)
    {
        ArgumentNullException.ThrowIfNull(panelUserStore);
        ArgumentNullException.ThrowIfNull(mfaCredentialStore);

        this._panelUserStore = panelUserStore;
        this._mfaCredentialStore = mfaCredentialStore;
    }

    /// <inheritdoc/>
    public Task<string> GetUserIdAsync(PanelUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(user.Id.ToString("D"));
    }

    /// <inheritdoc/>
    public Task<string?> GetUserNameAsync(PanelUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<string?>(user.DisplayName);
    }

    /// <inheritdoc/>
    public Task SetUserNameAsync(PanelUser user, string? userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        user.DisplayName = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string?> GetNormalizedUserNameAsync(PanelUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<string?>(user.DisplayName.ToUpperInvariant());
    }

    /// <inheritdoc/>
    public Task SetNormalizedUserNameAsync(PanelUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> CreateAsync(PanelUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        PanelUser created = await this._panelUserStore
            .CreateUserAsync(user, initialIdentity: null, cancellationToken)
            .ConfigureAwait(false);
        user.Id = created.Id;
        user.CreatedAtUtc = created.CreatedAtUtc;

        return IdentityResult.Success;
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> UpdateAsync(PanelUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        _ = await this._panelUserStore
            .UpdateUserAsync(user, cancellationToken)
            .ConfigureAwait(false);

        return IdentityResult.Success;
    }

    /// <inheritdoc/>
    public Task<IdentityResult> DeleteAsync(PanelUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "NotSupported",
            Description = "PanelUser deletion is not supported via PanelUserIdentityStore.",
        }));
    }

    /// <inheritdoc/>
    public async Task<PanelUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => !Guid.TryParse(userId, out Guid parsedUserId) || parsedUserId == Guid.Empty
            ? null
            : await this._panelUserStore
            .GetByIdAsync(parsedUserId, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<PanelUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return null;
        }

        IReadOnlyList<PanelUser> users = await this._panelUserStore
            .GetAllUsersAsync(cancellationToken)
            .ConfigureAwait(false);

        return users.FirstOrDefault(u =>
            string.Equals(u.DisplayName, normalizedUserName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async Task AddOrUpdatePasskeyAsync(
        PanelUser user,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(passkey);

        PasskeyCredentialPayload payload = ToPayload(passkey);
        await this._mfaCredentialStore
            .UpsertPasskeyAsync(user.Id, payload, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IList<UserPasskeyInfo>> GetPasskeysAsync(PanelUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        IReadOnlyList<PasskeyCredentialPayload> payloads = await this._mfaCredentialStore
            .GetPasskeysAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        return [.. payloads.Select(ToUserPasskeyInfo)];
    }

    /// <inheritdoc/>
    public async Task<PanelUser?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        (Guid PanelUserId, PasskeyCredentialPayload Passkey)? found = await this._mfaCredentialStore
            .FindPasskeyByCredentialIdAsync(credentialId, cancellationToken)
            .ConfigureAwait(false);

        return !found.HasValue
            ? null
            : await this._panelUserStore
            .GetByIdAsync(found.Value.PanelUserId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<UserPasskeyInfo?> FindPasskeyAsync(
        PanelUser user,
        byte[] credentialId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credentialId);

        PasskeyCredentialPayload? payload = await this._mfaCredentialStore
            .FindPasskeyAsync(user.Id, credentialId, cancellationToken)
            .ConfigureAwait(false);

        return payload is null ? null : ToUserPasskeyInfo(payload);
    }

    /// <inheritdoc/>
    public async Task RemovePasskeyAsync(
        PanelUser user,
        byte[] credentialId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credentialId);

        _ = await this._mfaCredentialStore
            .RemovePasskeyAsync(user.Id, credentialId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <summary>
    /// Converts an ASP.NET Core Identity <see cref="UserPasskeyInfo"/> into a <see cref="PasskeyCredentialPayload"/>.
    /// </summary>
    /// <param name="passkey">The Identity passkey descriptor.</param>
    /// <returns>The equivalent <see cref="PasskeyCredentialPayload"/>.</returns>
    private static PasskeyCredentialPayload ToPayload(UserPasskeyInfo passkey) =>
        new(
            passkey.CredentialId,
            passkey.PublicKey,
            passkey.CreatedAt,
            passkey.SignCount,
            passkey.Transports,
            passkey.IsUserVerified,
            passkey.IsBackupEligible,
            passkey.IsBackedUp,
            passkey.AttestationObject,
            passkey.ClientDataJson,
            passkey.Name);

    /// <summary>
    /// Converts a stored <see cref="PasskeyCredentialPayload"/> into an ASP.NET Core Identity <see cref="UserPasskeyInfo"/>.
    /// </summary>
    /// <param name="payload">The stored passkey credential payload.</param>
    /// <returns>The equivalent <see cref="UserPasskeyInfo"/>.</returns>
    private static UserPasskeyInfo ToUserPasskeyInfo(PasskeyCredentialPayload payload) =>
        new(
            payload.CredentialId,
            payload.PublicKey,
            payload.CreatedAt,
            payload.SignCount,
            payload.Transports,
            payload.IsUserVerified,
            payload.IsBackupEligible,
            payload.IsBackedUp,
            payload.AttestationObject,
            payload.ClientDataJson)
        {
            Name = payload.Name,
        };
}
