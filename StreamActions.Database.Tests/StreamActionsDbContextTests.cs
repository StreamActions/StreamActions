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

using Doka.EntityFrameworkCore.MySql;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Providers;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class StreamActionsDbContextTests
{
    #region OnModelCreating
    [Theory]
    [Trait("Member", "OnModelCreating")]
    [InlineData(DatabaseProvider.Sqlite)]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.MariaDb)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void OnModelCreating_ModelValidation_SucceedsForAllProviders(DatabaseProvider provider)
    {
        using StreamActionsDbContext context = CreateProviderContext(provider);

        IModelValidator validator = context.GetService<IModelValidator>();
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger = context.GetService<IDiagnosticsLogger<DbLoggerCategory.Model.Validation>>();
        IModel designTimeModel = context.GetService<IDesignTimeModel>().Model;

        Action validate = () => validator.Validate(designTimeModel, logger);
        _ = validate.Should().NotThrow();
    }

    [Fact]
    [Trait("Member", "OnConfiguring")]
    public void OnConfiguring_DefaultsToNoTracking()
    {
        using TestSqliteContextFactory factory = new();
        using StreamActionsDbContext context = factory.CreateDbContext();

        _ = context.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.NoTracking);
    }

    [Fact]
    [Trait("Member", "OnModelCreating")]
    public async Task CascadeDelete_WhenWorkspaceDeleted_DeletesOwnedEntities()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);

        Guid groupId = Guid.CreateVersion7();

        await using (StreamActionsDbContext writeContext = factory.CreateDbContext())
        {
            _ = writeContext.CustomCommands.Add(new CustomCommand
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Trigger = "!ping",
                AliasesJson = "[]",
                RoutineJson = "{}",
                IsEnabled = true,
            });

            _ = writeContext.PermissionGroups.Add(new PermissionGroup
            {
                Id = groupId,
                WorkspaceId = workspaceId,
                Name = "Moderators",
            });

            _ = writeContext.PermissionGrants.Add(new PermissionGrant
            {
                Id = Guid.CreateVersion7(),
                GroupId = groupId,
                PermissionKey = "commands.manage",
                State = PermissionState.Allow,
            });

            _ = await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StreamActionsDbContext deleteContext = factory.CreateDbContext())
        {
            Workspace workspace = await deleteContext.Workspaces
                .AsTracking()
                .FirstAsync(x => x.Id == workspaceId, TestContext.Current.CancellationToken);
            _ = deleteContext.Workspaces.Remove(workspace);
            _ = await deleteContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StreamActionsDbContext verifyContext = factory.CreateDbContext();
        _ = (await verifyContext.CustomCommands.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        _ = (await verifyContext.PermissionGroups.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        _ = (await verifyContext.PermissionGrants.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    private static StreamActionsDbContext CreateProviderContext(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => new SqliteStreamActionsDbContext(
            new DbContextOptionsBuilder<SqliteStreamActionsDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options),
        DatabaseProvider.Postgres => new PostgresStreamActionsDbContext(
            new DbContextOptionsBuilder<PostgresStreamActionsDbContext>()
                .UseNpgsql("Host=localhost;Database=streamactions;Username=sa;Password=sa")
                .Options),
        DatabaseProvider.MySql => new MySqlStreamActionsDbContext(
            new DbContextOptionsBuilder<MySqlStreamActionsDbContext>()
                .UseMySql(
                    "Server=localhost;Database=streamactions;Uid=sa;Pwd=sa;",
                    MySqlServerVersion.MySql(new Version(8, 4, 0)))
                .Options),
        DatabaseProvider.MariaDb => new MySqlStreamActionsDbContext(
            new DbContextOptionsBuilder<MySqlStreamActionsDbContext>()
                .UseMySql(
                    "Server=localhost;Database=streamactions;Uid=sa;Pwd=sa;",
                    MySqlServerVersion.MariaDb(new Version(11, 8, 0)))
                .Options),
        DatabaseProvider.SqlServer => new SqlServerStreamActionsDbContext(
            new DbContextOptionsBuilder<SqlServerStreamActionsDbContext>()
                .UseSqlServer("Server=localhost;Database=streamactions;Trusted_Connection=True;")
                .Options),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };
    #endregion
}
