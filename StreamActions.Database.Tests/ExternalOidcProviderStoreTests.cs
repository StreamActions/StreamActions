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
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Security;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class ExternalOidcProviderStoreTests
{
    #region UpsertAsync

    [Fact]
    [Trait("Member", "UpsertAsync")]
    public async Task UpsertAndTryGetClientSecretAsync_EncryptsSecretAndIsolatesInstanceVsWorkspace()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        SecretProtectionService protectionA = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        ExternalOidcProviderStore storeA = new(factory, protectionA);

        ExternalOidcProvider instanceProvider = await storeA.UpsertAsync(
            new ExternalOidcProvider
            {
                WorkspaceId = null,
                Slug = "Authentik",
                DisplayName = "Company SSO",
                AuthorityUrl = "https://sso.example.com",
                ClientId = "inst-oidc-client",
                ScopesJson = "[\"openid\",\"profile\"]",
                GroupClaimType = "groups",
                IsEnabled = true,
            },
            "instance-secret",
            ct);

        ExternalOidcProvider workspaceProvider = await storeA.UpsertAsync(
            new ExternalOidcProvider
            {
                WorkspaceId = workspaceId,
                Slug = "authentik",
                DisplayName = "Workspace SSO",
                AuthorityUrl = "https://ws-sso.example.com",
                ClientId = "ws-oidc-client",
                ScopesJson = "[\"openid\"]",
                IsEnabled = true,
            },
            "workspace-secret",
            ct);

        _ = instanceProvider.Slug.Should().Be("authentik");
        _ = instanceProvider.EncryptedClientSecret.Should().NotContain("instance-secret");

        (bool okInst, string? secretInst, CredentialStatus statusInst) = await storeA.TryGetClientSecretAsync(instanceProvider.Id, ct);
        (bool okWs, string? secretWs, CredentialStatus statusWs) = await storeA.TryGetClientSecretAsync(workspaceProvider.Id, ct);

        _ = okInst.Should().BeTrue();
        _ = secretInst.Should().Be("instance-secret");
        _ = statusInst.Should().Be(CredentialStatus.Active);
        _ = okWs.Should().BeTrue();
        _ = secretWs.Should().Be("workspace-secret");
        _ = statusWs.Should().Be(CredentialStatus.Active);

        ExternalOidcProvider? bySlug = await storeA.GetBySlugAsync(null, "AUTHENTIK", ct);
        _ = bySlug.Should().NotBeNull();
        _ = bySlug!.Id.Should().Be(instanceProvider.Id);

        SecretProtectionService protectionB = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        ExternalOidcProviderStore storeB = new(factory, protectionB);

        (bool okMismatch, string? secretMismatch, CredentialStatus statusMismatch) = await storeB.TryGetClientSecretAsync(instanceProvider.Id, ct);
        _ = okMismatch.Should().BeFalse();
        _ = secretMismatch.Should().BeNull();
        _ = statusMismatch.Should().Be(CredentialStatus.KeyRingMismatchRequiresReauth);
    }

    #endregion
}
