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
using StreamActions.Database.Security;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class SecretProtectionServiceTests
{
    #region ProtectWorkspaceToken
    [Fact]
    [Trait("Member", "ProtectWorkspaceToken")]
    public void ProtectAndUnprotect_WorkspaceToken_RoundTrips()
    {
        EphemeralDataProtectionProvider provider = new();
        SecretProtectionService service = new(provider, NullLogger<SecretProtectionService>.Instance);
        Guid workspaceId = Guid.CreateVersion7();

        string protectedPayload = service.ProtectWorkspaceToken(workspaceId, "twitch", TokenRole.Broadcaster, "oauth_secret_payload");
        (bool success, string? plaintext, CredentialStatus status) = service.TryUnprotectWorkspaceToken(workspaceId, "twitch", TokenRole.Broadcaster, protectedPayload);

        _ = success.Should().BeTrue();
        _ = plaintext.Should().Be("oauth_secret_payload");
        _ = status.Should().Be(CredentialStatus.Active);
    }

    [Fact]
    [Trait("Member", "TryUnprotectWorkspaceToken")]
    public void TryUnprotectWorkspaceToken_CrossWorkspaceAadMismatch_ReturnsKeyRingMismatchRequiresReauth()
    {
        EphemeralDataProtectionProvider provider = new();
        SecretProtectionService service = new(provider, NullLogger<SecretProtectionService>.Instance);
        Guid workspaceA = Guid.CreateVersion7();
        Guid workspaceB = Guid.CreateVersion7();

        string protectedPayload = service.ProtectWorkspaceToken(workspaceA, "twitch", TokenRole.BotChat, "bot_token");
        (bool success, string? plaintext, CredentialStatus status) = service.TryUnprotectWorkspaceToken(workspaceB, "twitch", TokenRole.BotChat, protectedPayload);

        _ = success.Should().BeFalse();
        _ = plaintext.Should().BeNull();
        _ = status.Should().Be(CredentialStatus.KeyRingMismatchRequiresReauth);
    }
    #endregion

    #region ProtectWorkspaceSecret
    [Fact]
    [Trait("Member", "TryUnprotectWorkspaceSecret")]
    public void TryUnprotectWorkspaceSecret_WhenKeyRingReplaced_ReturnsKeyRingMismatchRequiresReauth()
    {
        EphemeralDataProtectionProvider originalProvider = new();
        SecretProtectionService originalService = new(originalProvider, NullLogger<SecretProtectionService>.Instance);
        Guid workspaceId = Guid.CreateVersion7();

        string protectedPayload = originalService.ProtectWorkspaceSecret(workspaceId, "api_key", "super-secret-value");

        EphemeralDataProtectionProvider replacedProvider = new();
        SecretProtectionService replacedService = new(replacedProvider, NullLogger<SecretProtectionService>.Instance);

        (bool success, string? plaintext, CredentialStatus status) = replacedService.TryUnprotectWorkspaceSecret(workspaceId, "api_key", protectedPayload);

        _ = success.Should().BeFalse();
        _ = plaintext.Should().BeNull();
        _ = status.Should().Be(CredentialStatus.KeyRingMismatchRequiresReauth);
    }
    #endregion

    #region ProtectInstanceOAuth
    [Fact]
    [Trait("Member", "ProtectInstanceOAuth")]
    public void ProtectAndUnprotect_InstanceOAuth_RoundTrips()
    {
        EphemeralDataProtectionProvider provider = new();
        SecretProtectionService service = new(provider, NullLogger<SecretProtectionService>.Instance);

        string protectedPayload = service.ProtectInstanceOAuth("twitch", "{\"clientId\":\"abc\"}");
        (bool success, string? plaintext, CredentialStatus status) = service.TryUnprotectInstanceOAuth("twitch", protectedPayload);

        _ = success.Should().BeTrue();
        _ = plaintext.Should().Be("{\"clientId\":\"abc\"}");
        _ = status.Should().Be(CredentialStatus.Active);
    }
    #endregion
}
