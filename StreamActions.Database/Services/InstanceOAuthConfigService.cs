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

using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Security;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace StreamActions.Database.Services;

/// <summary>
/// Manages encrypted instance-wide platform OAuth application credentials stored in <see cref="InstanceSettings"/>.
/// </summary>
public sealed class InstanceOAuthConfigService : IInstanceOAuthConfigService, IDisposable
{
    /// <summary>
    /// Data Protection provider identifier used to bind the serialized platform OAuth configuration dictionary.
    /// </summary>
    private const string _protectionProviderId = "platform-oauth-configs";

    /// <summary>
    /// JSON serializer options used for persisting platform OAuth credential maps.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Instance settings service managing the underlying <see cref="InstanceSettings"/> row.
    /// </summary>
    private readonly IInstanceSettingsService _instanceSettingsService;

    /// <summary>
    /// Secret protection service used to encrypt and decrypt the configuration payload.
    /// </summary>
    private readonly SecretProtectionService _secretProtection;

    /// <summary>
    /// Synchronization gate serializing read-modify-write updates to the encrypted configuration payload.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceOAuthConfigService"/> class.
    /// </summary>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="secretProtection">The secret protection service.</param>
    public InstanceOAuthConfigService(
        IInstanceSettingsService instanceSettingsService,
        SecretProtectionService secretProtection)
    {
        ArgumentNullException.ThrowIfNull(instanceSettingsService);
        ArgumentNullException.ThrowIfNull(secretProtection);

        this._instanceSettingsService = instanceSettingsService;
        this._secretProtection = secretProtection;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, PlatformOAuthAppCredentials> map = this.DeserializeConfigs(settings.EncryptedPlatformOAuthConfigJson);
        return new ReadOnlyDictionary<string, PlatformOAuthAppCredentials>(map);
    }

    /// <inheritdoc/>
    public async ValueTask<PlatformOAuthAppCredentials?> GetAsync(string platformId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, PlatformOAuthAppCredentials> map = this.DeserializeConfigs(settings.EncryptedPlatformOAuthConfigJson);

        return map.GetValueOrDefault(normalizedPlatformId);
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync(
        string platformId,
        PlatformOAuthAppCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentials.ClientId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        string? normalizedSecret = string.IsNullOrWhiteSpace(credentials.ClientSecret)
            ? null
            : credentials.ClientSecret.Trim();
        PlatformOAuthAppCredentials normalizedCredentials = new(
            credentials.ClientId.Trim(),
            normalizedSecret,
            credentials.RedirectUri);

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            InstanceSettings current = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            Dictionary<string, PlatformOAuthAppCredentials> map = this.DeserializeConfigs(current.EncryptedPlatformOAuthConfigJson);
            map[normalizedPlatformId] = normalizedCredentials;

            string json = JsonSerializer.Serialize(map, _jsonOptions);
            string protectedPayload = this._secretProtection.ProtectInstanceOAuth(_protectionProviderId, json);
            InstanceSettings updated = CloneWithEncryptedOAuthConfig(current, protectedPayload);

            await this._instanceSettingsService.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = this._gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(string platformId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);

        await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            InstanceSettings current = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            Dictionary<string, PlatformOAuthAppCredentials> map = this.DeserializeConfigs(current.EncryptedPlatformOAuthConfigJson);

            if (!map.Remove(normalizedPlatformId))
            {
                return false;
            }

            string? protectedPayload = map.Count == 0
                ? null
                : this._secretProtection.ProtectInstanceOAuth(_protectionProviderId, JsonSerializer.Serialize(map, _jsonOptions));

            InstanceSettings updated = CloneWithEncryptedOAuthConfig(current, protectedPayload);
            await this._instanceSettingsService.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _ = this._gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => this._gate.Dispose();

    /// <summary>
    /// Normalizes a platform identifier to trimmed lowercase invariant form.
    /// </summary>
    /// <param name="platformId">The raw platform identifier.</param>
    /// <returns>The normalized platform identifier.</returns>
    private static string NormalizePlatformId(string platformId) => platformId.Trim().ToLowerInvariant();

    /// <summary>
    /// Clones an <see cref="InstanceSettings"/> instance with a new encrypted OAuth configuration payload.
    /// </summary>
    /// <param name="source">The source instance settings.</param>
    /// <param name="encryptedPlatformOAuthConfigJson">The replacement encrypted payload, or <see langword="null"/>.</param>
    /// <returns>A cloned <see cref="InstanceSettings"/> instance ready for persistence.</returns>
    private static InstanceSettings CloneWithEncryptedOAuthConfig(
        InstanceSettings source,
        string? encryptedPlatformOAuthConfigJson) =>
        new()
        {
            Id = source.Id,
            AnonymousInstallId = source.AnonymousInstallId,
            SentryOptIn = source.SentryOptIn,
            SentryCustomDsn = source.SentryCustomDsn,
            SentryEnabled = source.SentryEnabled,
            SentryTracesSampleRate = source.SentryTracesSampleRate,
            LogLevelsJson = source.LogLevelsJson,
            BackupIntervalHours = source.BackupIntervalHours,
            MaxBackupFiles = source.MaxBackupFiles,
            InstanceName = source.InstanceName,
            TotpIssuerName = source.TotpIssuerName,
            InstanceOAuthAppPolicy = source.InstanceOAuthAppPolicy,
            PlatformOAuthAppPoliciesJson = source.PlatformOAuthAppPoliciesJson,
            InstanceBotAccountPolicy = source.InstanceBotAccountPolicy,
            EncryptedPlatformOAuthConfigJson = encryptedPlatformOAuthConfigJson,
            AllowOpenRegistration = source.AllowOpenRegistration,
            ExternalOidcLoginMode = source.ExternalOidcLoginMode,
            MfaPasskeyAvailability = source.MfaPasskeyAvailability,
            MfaTotpAvailability = source.MfaTotpAvailability,
            LoginMfaRequirement = source.LoginMfaRequirement,
            StepUpSudoPolicy = source.StepUpSudoPolicy,
            UpdatedAtUtc = source.UpdatedAtUtc,
        };

    /// <summary>
    /// Decrypts and deserializes the stored platform OAuth credential dictionary.
    /// </summary>
    /// <param name="encryptedPayload">The protected payload from <see cref="InstanceSettings"/>.</param>
    /// <returns>A dictionary keyed by normalized lowercase platform identifier.</returns>
    private Dictionary<string, PlatformOAuthAppCredentials> DeserializeConfigs(string? encryptedPayload)
    {
        Dictionary<string, PlatformOAuthAppCredentials> normalized = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(encryptedPayload))
        {
            return normalized;
        }

        (bool success, string? plaintext, CredentialStatus _) = this._secretProtection.TryUnprotectInstanceOAuth(
            _protectionProviderId,
            encryptedPayload);

        if (!success || string.IsNullOrWhiteSpace(plaintext))
        {
            return normalized;
        }

        Dictionary<string, PlatformOAuthAppCredentials>? raw =
            JsonSerializer.Deserialize<Dictionary<string, PlatformOAuthAppCredentials>>(plaintext, _jsonOptions);

        if (raw is null)
        {
            return normalized;
        }

        foreach (KeyValuePair<string, PlatformOAuthAppCredentials> entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)
                || entry.Value is null
                || string.IsNullOrWhiteSpace(entry.Value.ClientId))
            {
                continue;
            }

            string? normalizedSecret = string.IsNullOrWhiteSpace(entry.Value.ClientSecret)
                ? null
                : entry.Value.ClientSecret.Trim();

            normalized[NormalizePlatformId(entry.Key)] = new PlatformOAuthAppCredentials(
                entry.Value.ClientId.Trim(),
                normalizedSecret,
                entry.Value.RedirectUri);
        }

        return normalized;
    }
}
