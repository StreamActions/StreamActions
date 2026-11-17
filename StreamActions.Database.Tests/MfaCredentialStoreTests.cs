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

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Database.Security;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

/// <summary>
/// Unit tests for <see cref="MfaCredentialStore"/>.
/// </summary>
public sealed class MfaCredentialStoreTests
{
    #region SaveTotpAsync

    /// <summary>
    /// Verifies that <see cref="MfaCredentialStore.SaveTotpAsync"/> encrypts the TOTP secret at rest and replaces any prior TOTP credential for the user.
    /// </summary>
    [Fact]
    [Trait("Member", "SaveTotpAsync")]
    public async Task SaveTotpAsync_EncryptsSecretAtRestAndReplacesExistingTotpRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        TotpService totpService = new(timeProvider);
        SecretProtectionService protection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        MfaCredentialStore store = new(factory, protection, totpService, timeProvider);

        string firstSecret = totpService.GenerateSecretBase32();
        string secondSecret = totpService.GenerateSecretBase32();
        IReadOnlyList<string> hashes = [.. totpService.GenerateRecoveryCodes().Select(totpService.HashRecoveryCode)];

        Guid firstId = await store.SaveTotpAsync(userId, new TotpSecretPayload(firstSecret, "First Phone"), hashes, ct);
        Guid secondId = await store.SaveTotpAsync(userId, new TotpSecretPayload(secondSecret, "Backup Phone"), hashes, ct);

        TotpSecretPayload? loaded = await store.GetTotpAsync(userId, ct);

        _ = secondId.Should().NotBe(firstId);
        _ = loaded.Should().NotBeNull();
        _ = loaded.SecretBase32.Should().Be(secondSecret);
        _ = loaded.Label.Should().Be("Backup Phone");
        _ = (await store.HasAnyCredentialsAsync(userId, ct)).Should().BeTrue();

        StreamActionsDbContext db = factory.CreateDbContext();
        await using (db.ConfigureAwait(false))
        {
            List<PanelUserMfaCredential> rows = await db.PanelUserMfaCredentials
                .Where(x => x.PanelUserId == userId && x.Method == MfaMethod.Totp)
                .ToListAsync(ct);

            _ = rows.Should().ContainSingle();
            _ = rows[0].CredentialDataEncrypted.Should().NotContain(secondSecret);
            _ = rows[0].CredentialDataEncrypted.Should().NotContain(firstSecret);
            _ = rows[0].RecoveryCodesHashesJson.Should().NotBeNullOrWhiteSpace();
        }
    }

    #endregion

    #region VerifyAndConsumeRecoveryCodeAsync

    /// <summary>
    /// Verifies that <see cref="MfaCredentialStore.VerifyAndConsumeRecoveryCodeAsync"/> consumes a valid recovery code once and rejects subsequent reuse.
    /// </summary>
    [Fact]
    [Trait("Member", "VerifyAndConsumeRecoveryCodeAsync")]
    public async Task VerifyAndConsumeRecoveryCodeAsync_ConsumesMatchedCodeOnceAndUpdatesRemainingCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        TotpService totpService = new(timeProvider);
        SecretProtectionService protection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        MfaCredentialStore store = new(factory, protection, totpService, timeProvider);

        IReadOnlyList<string> plaintextCodes = totpService.GenerateRecoveryCodes();
        IReadOnlyList<string> hashes = [.. plaintextCodes.Select(totpService.HashRecoveryCode)];
        _ = await store.SaveTotpAsync(userId, new TotpSecretPayload(totpService.GenerateSecretBase32()), hashes, ct);

        _ = (await store.GetRemainingRecoveryCodesCountAsync(userId, ct)).Should().Be(10);

        string stripped = plaintextCodes[3].Replace("-", string.Empty, StringComparison.Ordinal);
        string normalizedCode = string.Concat(stripped.Select(char.ToLowerInvariant));
        bool firstVerify = await store.VerifyAndConsumeRecoveryCodeAsync(userId, normalizedCode, ct);
        bool secondVerify = await store.VerifyAndConsumeRecoveryCodeAsync(userId, plaintextCodes[3], ct);
        bool invalidVerify = await store.VerifyAndConsumeRecoveryCodeAsync(userId, "INVALID-CODE", ct);

        _ = firstVerify.Should().BeTrue();
        _ = secondVerify.Should().BeFalse();
        _ = invalidVerify.Should().BeFalse();
        _ = (await store.GetRemainingRecoveryCodesCountAsync(userId, ct)).Should().Be(9);
    }

    #endregion

    #region UpsertPasskeyAsync

    /// <summary>
    /// Verifies that <see cref="MfaCredentialStore.UpsertPasskeyAsync"/> inserts a new passkey and updates an existing matching passkey in place.
    /// </summary>
    [Fact]
    [Trait("Member", "UpsertPasskeyAsync")]
    public async Task UpsertPasskeyAsync_InsertsAndUpdatesSignCountWithoutDuplicating()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        TotpService totpService = new(timeProvider);
        SecretProtectionService protection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        MfaCredentialStore store = new(factory, protection, totpService, timeProvider);

        byte[] credentialId = [1, 2, 3, 4, 5, 6];
        PasskeyCredentialPayload initial = CreatePasskey(credentialId, signCount: 1, name: "YubiKey 5");
        await store.UpsertPasskeyAsync(userId, initial, ct);

        PasskeyCredentialPayload updated = initial with { SignCount = 42, Name = "YubiKey 5C" };
        await store.UpsertPasskeyAsync(userId, updated, ct);

        IReadOnlyList<PasskeyCredentialPayload> passkeys = await store.GetPasskeysAsync(userId, ct);

        _ = passkeys.Should().ContainSingle();
        _ = passkeys[0].SignCount.Should().Be(42U);
        _ = passkeys[0].Name.Should().Be("YubiKey 5C");
    }

    #endregion

    #region FindPasskeyByCredentialIdAsync

    /// <summary>
    /// Verifies that <see cref="MfaCredentialStore.FindPasskeyAsync"/> and <see cref="MfaCredentialStore.FindPasskeyByCredentialIdAsync"/> resolve passkeys and safely skip undecryptable rows.
    /// </summary>
    [Fact]
    [Trait("Member", "FindPasskeyByCredentialIdAsync")]
    public async Task FindPasskeyByCredentialIdAsync_ResolvesMatchAndSkipsKeyRingMismatchRows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (Guid userIdA, _) = await factory.SeedWorkspaceAsync(ct);
        (Guid userIdB, _) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        TotpService totpService = new(timeProvider);
        SecretProtectionService protectionA = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        SecretProtectionService protectionB = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);

        MfaCredentialStore storeA = new(factory, protectionA, totpService, timeProvider);
        MfaCredentialStore storeB = new(factory, protectionB, totpService, timeProvider);

        byte[] undecryptableCredId = [10, 20, 30];
        byte[] decryptableCredId = [40, 50, 60];

        await storeA.UpsertPasskeyAsync(userIdA, CreatePasskey(undecryptableCredId, 1, "Old Key"), ct);
        await storeB.UpsertPasskeyAsync(userIdB, CreatePasskey(decryptableCredId, 7, "Current Key"), ct);

        PasskeyCredentialPayload? scopedMatch = await storeB.FindPasskeyAsync(userIdB, decryptableCredId, ct);
        (Guid PanelUserId, PasskeyCredentialPayload Passkey)? globalMatch = await storeB.FindPasskeyByCredentialIdAsync(decryptableCredId, ct);
        (Guid PanelUserId, PasskeyCredentialPayload Passkey)? missingMatch = await storeB.FindPasskeyByCredentialIdAsync(undecryptableCredId, ct);

        _ = scopedMatch.Should().NotBeNull();
        _ = scopedMatch.Name.Should().Be("Current Key");
        _ = globalMatch.Should().NotBeNull();
        _ = globalMatch.Value.PanelUserId.Should().Be(userIdB);
        _ = globalMatch.Value.Passkey.SignCount.Should().Be(7U);
        _ = missingMatch.Should().BeNull();
    }

    #endregion

    #region GetSummariesAsync

    /// <summary>
    /// Verifies that <see cref="MfaCredentialStore.GetSummariesAsync"/> returns safe metadata and falls back to default display names if decryption fails.
    /// </summary>
    [Fact]
    [Trait("Member", "GetSummariesAsync")]
    public async Task GetSummariesAsync_ReturnsSafeSummariesAndFallsBackWhenKeyRingMismatches()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        TotpService totpService = new(timeProvider);
        SecretProtectionService protectionA = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        SecretProtectionService protectionB = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);

        MfaCredentialStore storeA = new(factory, protectionA, totpService, timeProvider);
        MfaCredentialStore storeB = new(factory, protectionB, totpService, timeProvider);

        IReadOnlyList<string> hashes = [.. totpService.GenerateRecoveryCodes().Select(totpService.HashRecoveryCode)];
        _ = await storeA.SaveTotpAsync(userId, new TotpSecretPayload(totpService.GenerateSecretBase32(), "Work Phone"), hashes, ct);
        await storeA.UpsertPasskeyAsync(userId, CreatePasskey([9, 8, 7], 1, "Laptop TouchID"), ct);

        IReadOnlyList<MfaCredentialSummary> readableSummaries = await storeA.GetSummariesAsync(userId, ct);
        IReadOnlyList<MfaCredentialSummary> fallbackSummaries = await storeB.GetSummariesAsync(userId, ct);

        _ = readableSummaries.Should().HaveCount(2);
        _ = readableSummaries.Should().ContainSingle(s => s.Method == MfaMethod.Totp && s.DisplayName == "Work Phone" && s.RemainingRecoveryCodesCount == 10);
        _ = readableSummaries.Should().ContainSingle(s => s.Method == MfaMethod.Passkey && s.DisplayName == "Laptop TouchID" && s.RemainingRecoveryCodesCount == 0);

        _ = fallbackSummaries.Should().HaveCount(2);
        _ = fallbackSummaries.Should().ContainSingle(s => s.Method == MfaMethod.Totp && s.DisplayName == "Authenticator App" && s.RemainingRecoveryCodesCount == 10);
        _ = fallbackSummaries.Should().ContainSingle(s => s.Method == MfaMethod.Passkey && s.DisplayName == "Passkey" && s.RemainingRecoveryCodesCount == 0);
    }

    #endregion

    #region ClearAllForUserAsync

    /// <summary>
    /// Verifies that <see cref="MfaCredentialStore.RemovePasskeyAsync"/>, <see cref="MfaCredentialStore.DeleteCredentialAsync"/>, and <see cref="MfaCredentialStore.ClearAllForUserAsync"/> delete only the targeted credentials.
    /// </summary>
    [Fact]
    [Trait("Member", "ClearAllForUserAsync")]
    public async Task RemovePasskeyDeleteCredentialAndClearAllForUserAsync_RemoveExpectedRows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(ct);
        (Guid otherUserId, _) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        TotpService totpService = new(timeProvider);
        SecretProtectionService protection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        MfaCredentialStore store = new(factory, protection, totpService, timeProvider);

        byte[] passkeyId1 = [1, 1, 1];
        byte[] passkeyId2 = [2, 2, 2];
        _ = await store.SaveTotpAsync(userId, new TotpSecretPayload(totpService.GenerateSecretBase32(), "Phone"), [], ct);
        await store.UpsertPasskeyAsync(userId, CreatePasskey(passkeyId1, 1, "Key 1"), ct);
        await store.UpsertPasskeyAsync(userId, CreatePasskey(passkeyId2, 1, "Key 2"), ct);
        _ = await store.SaveTotpAsync(otherUserId, new TotpSecretPayload(totpService.GenerateSecretBase32(), "Other"), [], ct);

        _ = (await store.RemovePasskeyAsync(userId, passkeyId1, ct)).Should().BeTrue();
        _ = (await store.RemovePasskeyAsync(userId, passkeyId1, ct)).Should().BeFalse();

        IReadOnlyList<MfaCredentialSummary> remaining = await store.GetSummariesAsync(userId, ct);
        _ = remaining.Should().HaveCount(2);
        Guid passkeyRowId = remaining.Single(s => s.Method == MfaMethod.Passkey).Id;

        _ = (await store.DeleteCredentialAsync(userId, passkeyRowId, ct)).Should().BeTrue();
        int cleared = await store.ClearAllForUserAsync(userId, ct);

        _ = cleared.Should().Be(1);
        _ = (await store.HasAnyCredentialsAsync(userId, ct)).Should().BeFalse();
        _ = (await store.HasAnyCredentialsAsync(otherUserId, ct)).Should().BeTrue();
    }

    #endregion

    /// <summary>
    /// Creates a sample <see cref="PasskeyCredentialPayload"/> for testing.
    /// </summary>
    /// <param name="credentialId">The WebAuthn credential identifier bytes.</param>
    /// <param name="signCount">The signature counter.</param>
    /// <param name="name">The friendly passkey display name.</param>
    /// <returns>A populated <see cref="PasskeyCredentialPayload"/>.</returns>
    private static PasskeyCredentialPayload CreatePasskey(byte[] credentialId, uint signCount, string name) =>
        new(
            credentialId,
            [9, 9, 9],
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            signCount,
            ["internal", "hybrid"],
            true,
            true,
            true,
            [7, 7, 7],
            [8, 8, 8],
            name);
}
