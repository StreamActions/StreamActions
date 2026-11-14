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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Services;
using System.Text;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class WorkspacePortableArchiveServiceTests
{
    #region ExportAsync
    [Fact]
    [Trait("Member", "ExportAsync")]
    public async Task ExportAndImport_RoundTripsWorkspaceData_WithoutLeakingSecretsOrTokens()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid sourceWorkspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        (_, Guid targetWorkspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Guid groupId = Guid.CreateVersion7();
        const string encryptedSecretValue = "SUPER_SENSITIVE_ENCRYPTED_SECRET_VALUE";
        const string encryptedTokenJson = "SUPER_SENSITIVE_OAUTH_TOKEN_JSON";

        await using (StreamActionsDbContext db = factory.CreateDbContext())
        {
            _ = db.CustomCommands.Add(new CustomCommand
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = sourceWorkspaceId,
                Trigger = "!lurk",
                AliasesJson = "[\"!brb\"]",
                RoutineJson = "{\"steps\":[]}",
                IsEnabled = true,
            });

            _ = db.PermissionGroups.Add(new PermissionGroup
            {
                Id = groupId,
                WorkspaceId = sourceWorkspaceId,
                Name = "VIP",
            });

            _ = db.PermissionGrants.Add(new PermissionGrant
            {
                Id = Guid.CreateVersion7(),
                GroupId = groupId,
                PermissionKey = "command.lurk",
                State = PermissionState.Allow,
            });

            _ = db.WorkspaceSecrets.Add(new WorkspaceSecret
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = sourceWorkspaceId,
                SecretName = "external_api_key",
                EncryptedValue = encryptedSecretValue,
                AllowedHostPatternsJson = "[\"api.example.com\"]",
                Status = CredentialStatus.Active,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            _ = db.PlatformCredentials.Add(new PlatformCredential
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = sourceWorkspaceId,
                PlatformId = "twitch",
                TokenRole = TokenRole.Broadcaster,
                EncryptedTokenJson = encryptedTokenJson,
                Status = CredentialStatus.Active,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            _ = await db.SaveChangesAsync(ct);
        }

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero));
        WorkspacePortableArchiveService service = new(factory, timeProvider);

        MemoryStream gzipStream = new();
        await using (gzipStream.ConfigureAwait(false))
        {
            await service.ExportGzipAsync(sourceWorkspaceId, gzipStream, ct);

            byte[] compressedBytes = gzipStream.ToArray();
            string rawCompressedAscii = Encoding.UTF8.GetString(compressedBytes);
            _ = rawCompressedAscii.Should().NotContain(encryptedSecretValue);
            _ = rawCompressedAscii.Should().NotContain(encryptedTokenJson);

            gzipStream.Position = 0;
            await service.ImportGzipAsync(targetWorkspaceId, gzipStream, ct);
        }

        await using StreamActionsDbContext verifyDb = factory.CreateDbContext();
        CustomCommand? importedCommand = await verifyDb.CustomCommands
            .FirstOrDefaultAsync(x => (x.WorkspaceId == targetWorkspaceId) && (x.Trigger == "!lurk"), ct);
        PermissionGroup? importedGroup = await verifyDb.PermissionGroups
            .FirstOrDefaultAsync(x => (x.WorkspaceId == targetWorkspaceId) && (x.Name == "VIP"), ct);

        _ = importedCommand.Should().NotBeNull();
        _ = importedGroup.Should().NotBeNull();

        PermissionGrant? importedGrant = await verifyDb.PermissionGrants
            .FirstOrDefaultAsync(x => x.GroupId == importedGroup!.Id, ct);
        _ = importedGrant.Should().NotBeNull();
        _ = importedGrant!.PermissionKey.Should().Be("command.lurk");
        _ = (await verifyDb.WorkspaceSecrets.CountAsync(x => x.WorkspaceId == targetWorkspaceId, ct)).Should().Be(0);
        _ = (await verifyDb.PlatformCredentials.CountAsync(x => x.WorkspaceId == targetWorkspaceId, ct)).Should().Be(0);
    }
    #endregion
}
