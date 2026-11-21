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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Database.Security;
using StreamActions.Database.Services;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class SecretVaultTests
{
    #region SetSecretAsync

    [Fact]
    [Trait("Member", "SetSecretAsync")]
    public async Task SetSecretAsync_PersistsEncryptedPayloadWithoutPlaintextAndNormalizesName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        SecretRedactor redactor = new();
        SecretVault vault = CreateVault(factory, timeProvider, redactor);

        const string plaintext = "super-secret-weather-token-999";
        await vault.SetSecretAsync(
            workspaceId,
            "  Weather_API.Key-1  ",
            plaintext,
            ["API.Weather.com", "*.Sub.Weather.com"],
            cancellationToken: ct);

        StreamActionsDbContext db = factory.CreateDbContext();
        await using (db.ConfigureAwait(false))
        {
            WorkspaceSecret? row = await db.WorkspaceSecrets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId, ct);

            _ = row.Should().NotBeNull();
            _ = row!.SecretName.Should().Be("weather_api.key-1");
            _ = row.Status.Should().Be(CredentialStatus.Active);
            _ = row.EncryptedValue.Should().NotBeNullOrWhiteSpace();
            _ = row.EncryptedValue.Should().NotContain(plaintext);
            _ = row.AllowedHostPatternsJson.Should().Contain("api.weather.com");
            _ = row.AllowedHostPatternsJson.Should().Contain("*.sub.weather.com");
        }

        _ = redactor.Redact($"leak={plaintext}").Should().Be("leak=[REDACTED]");
    }

    [Theory]
    [Trait("Member", "SetSecretAsync")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/name")]
    [InlineData("bad name")]
    [InlineData("bad$name")]
    [InlineData("bad:name")]
    public async Task SetSecretAsync_WithInvalidSecretName_ThrowsArgumentException(string invalidName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        SecretVault vault = CreateVault(factory);

        Func<Task> act = async () => await vault.SetSecretAsync(
            workspaceId,
            invalidName,
            "secret-value",
            ["api.example.com"],
            cancellationToken: ct).ConfigureAwait(false);

        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    [Trait("Member", "SetSecretAsync")]
    public async Task SetSecretAsync_WithNameOver128Chars_ThrowsArgumentException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        SecretVault vault = CreateVault(factory);
        string tooLong = new('a', 129);

        Func<Task> act = async () => await vault.SetSecretAsync(
            workspaceId,
            tooLong,
            "secret-value",
            ["api.example.com"],
            cancellationToken: ct).ConfigureAwait(false);

        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [Trait("Member", "SetSecretAsync")]
    [InlineData("https://api.example.com")]
    [InlineData("api.example.com:8443")]
    [InlineData("api.example.com/v1")]
    [InlineData("user@api.example.com")]
    [InlineData("*")]
    [InlineData("*.*")]
    [InlineData("*.com")]
    [InlineData("api.*.example.com")]
    [InlineData("*.127.0.0.1")]
    [InlineData(".example.com")]
    [InlineData("example.com.")]
    [InlineData("   ")]
    public async Task SetSecretAsync_WithInvalidHostPattern_ThrowsArgumentException(string badPattern)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        SecretVault vault = CreateVault(factory);

        Func<Task> act = async () => await vault.SetSecretAsync(
            workspaceId,
            "api_key",
            "secret-value",
            [badPattern],
            cancellationToken: ct).ConfigureAwait(false);

        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    [Trait("Member", "SetSecretAsync")]
    public async Task SetSecretAsync_WithEmptyHostPatternList_ThrowsArgumentException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        SecretVault vault = CreateVault(factory);

        Func<Task> act = async () => await vault.SetSecretAsync(
            workspaceId,
            "api_key",
            "secret-value",
            [],
            cancellationToken: ct).ConfigureAwait(false);

        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetSecretValueAsync

    [Fact]
    [Trait("Member", "GetSecretValueAsync")]
    public async Task GetSecretValueAsync_IsolatesWorkspacesAndRespectsExpiry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(ct);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        SecretVault vault = CreateVault(factory, timeProvider);
        DateTimeOffset expiresAt = timeProvider.GetUtcNow().AddHours(1);

        await vault.SetSecretAsync(
            workspaceA,
            "api_key",
            "secret-alpha-1234",
            ["api.example.com"],
            expiresAt,
            ct);

        string? valueA = await vault.GetSecretValueAsync(workspaceA, "API_KEY", ct);
        string? valueB = await vault.GetSecretValueAsync(workspaceB, "api_key", ct);

        _ = valueA.Should().Be("secret-alpha-1234");
        _ = valueB.Should().BeNull();

        timeProvider.Advance(TimeSpan.FromHours(1));
        string? expiredValue = await vault.GetSecretValueAsync(workspaceA, "api_key", ct);
        _ = expiredValue.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetSecretValueAsync")]
    public async Task GetSecretValueAsync_WhenKeyRingMismatches_MarksStatusWithoutThrowing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        SecretVault vaultA = CreateVault(factory, timeProvider);
        await vaultA.SetSecretAsync(workspaceId, "api_key", "secret-val", ["api.example.com"], cancellationToken: ct);

        SecretVault vaultB = CreateVault(factory, timeProvider);
        string? value = await vaultB.GetSecretValueAsync(workspaceId, "api_key", ct);

        _ = value.Should().BeNull();

        IReadOnlyList<WorkspaceSecretMetadata> metadata = await vaultB.GetSecretsMetadataAsync(workspaceId, ct);
        _ = metadata.Should().ContainSingle();
        _ = metadata[0].Status.Should().Be(CredentialStatus.KeyRingMismatchRequiresReauth);
    }

    #endregion

    #region ResolveForHostAsync

    [Theory]
    [Trait("Member", "ResolveForHostAsync")]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "xUnit InlineData requires compile-time constant string URI inputs.")]
    [InlineData("https://api.weather.com/v1/forecast", SecretResolutionStatus.Resolved, "weather-secret-42")]
    [InlineData("https://v2.api.weather.com/v1/forecast", SecretResolutionStatus.Resolved, "weather-secret-42")]
    [InlineData("https://weather.com/v1/forecast", SecretResolutionStatus.HostNotAllowed, null)]
    [InlineData("https://evilweather.com/v1/forecast", SecretResolutionStatus.HostNotAllowed, null)]
    [InlineData("https://other.example.com/v1/forecast", SecretResolutionStatus.HostNotAllowed, null)]
    public async Task ResolveForHostAsync_EnforcesWildcardAndExactHostMatching(
        string url,
        SecretResolutionStatus expectedStatus,
        string? expectedPlaintext)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        SecretRedactor redactor = new();
        SecretVault vault = CreateVault(factory, redactor: redactor);
        await vault.SetSecretAsync(
            workspaceId,
            "weather_key",
            "weather-secret-42",
            ["*.weather.com"],
            cancellationToken: ct);

        SecretResolutionResult result = await vault.ResolveForHostAsync(
            workspaceId,
            "WEATHER_KEY",
            new Uri(url, UriKind.Absolute),
            ct);

        _ = result.Status.Should().Be(expectedStatus);
        _ = result.PlaintextValue.Should().Be(expectedPlaintext);
        _ = result.IsSuccess.Should().Be(expectedStatus == SecretResolutionStatus.Resolved);
    }

    [Fact]
    [Trait("Member", "ResolveForHostAsync")]
    public async Task ResolveForHostAsync_HandlesNotFoundRevokedExpiredAndKeyRingMismatch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        SecretVault vault = CreateVault(factory, timeProvider);
        Uri targetUri = new("https://api.example.com/data", UriKind.Absolute);

        SecretResolutionResult missing = await vault.ResolveForHostAsync(workspaceId, "missing", targetUri, ct);
        _ = missing.Status.Should().Be(SecretResolutionStatus.NotFound);
        _ = missing.PlaintextValue.Should().BeNull();

        await vault.SetSecretAsync(
            workspaceId,
            "expiring",
            "secret-exp",
            ["api.example.com"],
            timeProvider.GetUtcNow().AddMinutes(10),
            ct);

        timeProvider.Advance(TimeSpan.FromMinutes(10));
        SecretResolutionResult expired = await vault.ResolveForHostAsync(workspaceId, "expiring", targetUri, ct);
        _ = expired.Status.Should().Be(SecretResolutionStatus.Expired);
        _ = expired.PlaintextValue.Should().BeNull();

        await vault.SetSecretAsync(workspaceId, "revoked", "secret-rev", ["api.example.com"], cancellationToken: ct);
        StreamActionsDbContext db = factory.CreateDbContext();
        await using (db.ConfigureAwait(false))
        {
            WorkspaceSecret row = await db.WorkspaceSecrets.AsTracking().SingleAsync(x => x.SecretName == "revoked", ct);
            row.Status = CredentialStatus.Revoked;
            _ = await db.SaveChangesAsync(ct);
        }

        SecretResolutionResult revoked = await vault.ResolveForHostAsync(workspaceId, "revoked", targetUri, ct);
        _ = revoked.Status.Should().Be(SecretResolutionStatus.Revoked);
        _ = revoked.PlaintextValue.Should().BeNull();

        await vault.SetSecretAsync(workspaceId, "tampered", "secret-tamp", ["api.example.com"], cancellationToken: ct);
        StreamActionsDbContext db2 = factory.CreateDbContext();
        await using (db2.ConfigureAwait(false))
        {
            WorkspaceSecret row = await db2.WorkspaceSecrets.AsTracking().SingleAsync(x => x.SecretName == "tampered", ct);
            row.EncryptedValue = "not-valid-dataprotection-ciphertext";
            _ = await db2.SaveChangesAsync(ct);
        }

        SecretResolutionResult mismatched = await vault.ResolveForHostAsync(workspaceId, "tampered", targetUri, ct);
        _ = mismatched.Status.Should().Be(SecretResolutionStatus.KeyRingMismatch);
        _ = mismatched.PlaintextValue.Should().BeNull();

        SecretResolutionResult cachedMismatch = await vault.ResolveForHostAsync(workspaceId, "tampered", targetUri, ct);
        _ = cachedMismatch.Status.Should().Be(SecretResolutionStatus.KeyRingMismatch);
    }

    #endregion

    #region UpdateSecretPolicyAsync

    [Fact]
    [Trait("Member", "UpdateSecretPolicyAsync")]
    public async Task UpdateSecretPolicyAsync_AllowsUnchangedAndNarrowingWithoutRotationOrConfirmation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        SecretVault vault = CreateVault(factory);
        await vault.SetSecretAsync(
            workspaceId,
            "api_key",
            "original-secret",
            ["*.weather.com", "exact.other.com"],
            cancellationToken: ct);

        await vault.UpdateSecretPolicyAsync(
            workspaceId,
            "api_key",
            ["*.weather.com", "exact.other.com"],
            cancellationToken: ct);

        await vault.UpdateSecretPolicyAsync(
            workspaceId,
            "api_key",
            ["api.weather.com", "*.sub.weather.com"],
            cancellationToken: ct);

        IReadOnlyList<WorkspaceSecretMetadata> metadata = await vault.GetSecretsMetadataAsync(workspaceId, ct);
        _ = metadata.Should().ContainSingle();
        _ = metadata[0].AllowedHostPatterns.Should().BeEquivalentTo(["api.weather.com", "*.sub.weather.com"]);
        _ = (await vault.GetSecretValueAsync(workspaceId, "api_key", ct)).Should().Be("original-secret");
    }

    [Theory]
    [Trait("Member", "UpdateSecretPolicyAsync")]
    [InlineData(new[] { "api.weather.com" }, new[] { "api.weather.com", "evil.com" })]
    [InlineData(new[] { "api.weather.com" }, new[] { "*.weather.com" })]
    [InlineData(new[] { "*.api.weather.com" }, new[] { "*.weather.com" })]
    [InlineData(new[] { "*.weather.com" }, new[] { "weather.com" })]
    public async Task UpdateSecretPolicyAsync_BlocksHostExpansionWithoutRotationOrConfirmation(
        string[] initialPatterns,
        string[] expandedPatterns)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        SecretVault vault = CreateVault(factory);
        await vault.SetSecretAsync(workspaceId, "api_key", "original-secret", initialPatterns, cancellationToken: ct);

        Func<Task> act = async () => await vault.UpdateSecretPolicyAsync(
            workspaceId,
            "api_key",
            expandedPatterns,
            cancellationToken: ct).ConfigureAwait(false);

        _ = await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    [Trait("Member", "UpdateSecretPolicyAsync")]
    public async Task UpdateSecretPolicyAsync_AllowsHostExpansionWithRotationOrBroadcasterConfirmation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        SecretVault vault = CreateVault(factory);
        await vault.SetSecretAsync(workspaceId, "key_rotate", "secret-v1", ["api.example.com"], cancellationToken: ct);
        await vault.SetSecretAsync(workspaceId, "key_confirm", "secret-stay", ["api.example.com"], cancellationToken: ct);

        await vault.UpdateSecretPolicyAsync(
            workspaceId,
            "key_rotate",
            ["*.example.com", "partner.org"],
            newPlaintextValue: "secret-v2",
            cancellationToken: ct);

        await vault.UpdateSecretPolicyAsync(
            workspaceId,
            "key_confirm",
            ["*.example.com"],
            broadcasterConfirmed: true,
            cancellationToken: ct);

        _ = (await vault.GetSecretValueAsync(workspaceId, "key_rotate", ct)).Should().Be("secret-v2");
        _ = (await vault.GetSecretValueAsync(workspaceId, "key_confirm", ct)).Should().Be("secret-stay");
    }

    #endregion

    #region GetSecretsMetadataAsync

    [Fact]
    [Trait("Member", "GetSecretsMetadataAsync")]
    public async Task GetSecretsMetadataAsync_OmitsCiphertextAndComputesExpirationWithTimeProvider()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        SecretVault vault = CreateVault(factory, timeProvider);

        await vault.SetSecretAsync(workspaceId, "beta", "secret-beta", ["api.beta.com"], timeProvider.GetUtcNow().AddMinutes(5), ct);
        await vault.SetSecretAsync(workspaceId, "alpha", "secret-alpha", ["api.alpha.com"], null, ct);

        IReadOnlyList<WorkspaceSecretMetadata> before = await vault.GetSecretsMetadataAsync(workspaceId, ct);
        _ = before.Should().HaveCount(2);
        _ = before[0].SecretName.Should().Be("alpha");
        _ = before[0].IsExpired.Should().BeFalse();
        _ = before[0].HasSecretConfigured.Should().BeTrue();
        _ = before[1].SecretName.Should().Be("beta");
        _ = before[1].IsExpired.Should().BeFalse();

        timeProvider.Advance(TimeSpan.FromMinutes(6));
        IReadOnlyList<WorkspaceSecretMetadata> after = await vault.GetSecretsMetadataAsync(workspaceId, ct);
        _ = after[0].IsExpired.Should().BeFalse();
        _ = after[1].IsExpired.Should().BeTrue();
    }

    #endregion

    #region DeleteSecretAsync

    [Fact]
    [Trait("Member", "DeleteSecretAsync")]
    public async Task DeleteSecretAsync_DeletesOnlyMatchingWorkspaceSecret()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(ct);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(ct);

        SecretVault vault = CreateVault(factory);
        await vault.SetSecretAsync(workspaceA, "shared_name", "val-a", ["api.example.com"], cancellationToken: ct);
        await vault.SetSecretAsync(workspaceB, "shared_name", "val-b", ["api.example.com"], cancellationToken: ct);

        bool deletedMissing = await vault.DeleteSecretAsync(workspaceA, "nonexistent", ct);
        bool deletedA = await vault.DeleteSecretAsync(workspaceA, "SHARED_NAME", ct);

        _ = deletedMissing.Should().BeFalse();
        _ = deletedA.Should().BeTrue();
        _ = (await vault.GetSecretValueAsync(workspaceA, "shared_name", ct)).Should().BeNull();
        _ = (await vault.GetSecretValueAsync(workspaceB, "shared_name", ct)).Should().Be("val-b");
    }

    #endregion

    #region ServiceCollectionExtensions

    [Fact]
    [Trait("Member", "AddStreamActionsDatabase")]
    public void AddStreamActionsDatabase_RegistersISecretVaultAsSingleton()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:Database:Provider"] = "Sqlite",
                ["Bootstrap:Database:ConnectionString"] = "Data Source=:memory:",
            })
            .Build();

        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddDataProtection();
        _ = services.AddStreamActionsCore(configuration);
        _ = services.AddStreamActionsDatabase(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        ISecretVault first = provider.GetRequiredService<ISecretVault>();
        ISecretVault second = provider.GetRequiredService<ISecretVault>();

        _ = first.Should().BeOfType<SecretVault>();
        _ = second.Should().BeSameAs(first);
    }

    #endregion

    /// <summary>
    /// Creates a <see cref="SecretVault"/> backed by an isolated in-memory Data Protection provider.
    /// </summary>
    /// <param name="factory">The test SQLite context factory.</param>
    /// <param name="timeProvider">An optional time provider.</param>
    /// <param name="redactor">An optional secret redactor.</param>
    /// <returns>A configured <see cref="SecretVault"/>.</returns>
    private static SecretVault CreateVault(
        TestSqliteContextFactory factory,
        TimeProvider? timeProvider = null,
        ISecretRedactor? redactor = null)
    {
        SecretProtectionService protection = new(
            new EphemeralDataProtectionProvider(),
            NullLogger<SecretProtectionService>.Instance);

        return new(
            factory,
            protection,
            redactor ?? new SecretRedactor(),
            timeProvider ?? TimeProvider.System,
            NullLogger<SecretVault>.Instance);
    }
}
