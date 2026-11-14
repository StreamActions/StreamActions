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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Interceptors;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class DatabaseBackupServiceTests
{
    #region TriggerBackupAsync
    [Fact]
    [Trait("Member", "TriggerBackupAsync")]
    public async Task TriggerBackupAsync_CreatesSqliteBackupAndPrunesBeyondMaxBackupFiles()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"sa-backup-test-{Guid.NewGuid():N}");
        string dbPath = Path.Combine(tempRoot, "streamactions.db");
        string backupDir = Path.Combine(tempRoot, "backups");
        _ = Directory.CreateDirectory(tempRoot);

        try
        {
            DbContextOptionsBuilder<StreamActionsDbContext> dbOptionsBuilder = new();
            _ = StreamActionsDbContext.ConfigureDefaultOptions(dbOptionsBuilder)
                .UseSqlite($"Data Source={dbPath}")
                .AddInterceptors(new SqliteConnectionInterceptor());
            DbContextOptions<StreamActionsDbContext> dbOptions = dbOptionsBuilder.Options;

            TestFileDbContextFactory contextFactory = new(dbOptions);
            await using (StreamActionsDbContext initContext = contextFactory.CreateDbContext())
            {
                _ = await initContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                _ = initContext.InstanceSettings.Add(new InstanceSettings
                {
                    Id = Guid.CreateVersion7(),
                    AnonymousInstallId = Guid.NewGuid(),
                    BackupIntervalHours = 1,
                    MaxBackupFiles = 2,
                    InstanceOAuthAppPolicy = "instance_default",
                    InstanceBotAccountPolicy = "instance_default",
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });
                _ = await initContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            using InstanceSettingsService settingsService = new(contextFactory, timeProvider);
            IOptions<BootstrapOptions> bootstrapOptions = Options.Create(new BootstrapOptions
            {
                Database = new DatabaseBootstrapOptions
                {
                    Provider = DatabaseProvider.Sqlite,
                    ConnectionString = $"Data Source={dbPath}",
                },
                Paths = new PathBootstrapOptions
                {
                    BackupDir = backupDir,
                },
            });

            using DatabaseBackupService backupService = new(
                contextFactory,
                settingsService,
                bootstrapOptions,
                timeProvider,
                NullLogger<DatabaseBackupService>.Instance);

            string first = await backupService.TriggerBackupAsync(TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromHours(1));
            string second = await backupService.TriggerBackupAsync(TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromHours(1));
            string third = await backupService.TriggerBackupAsync(TestContext.Current.CancellationToken);

            _ = File.Exists(first).Should().BeFalse();
            _ = File.Exists(second).Should().BeTrue();
            _ = File.Exists(third).Should().BeTrue();
            _ = Directory.GetFiles(backupDir, "streamactions-backup-*.db").Should().HaveCount(2);

            SqliteConnection verifyConn = new($"Data Source={third};Pooling=False");
            await using (verifyConn.ConfigureAwait(false))
            {
                await verifyConn.OpenAsync(TestContext.Current.CancellationToken);
                SqliteCommand cmd = verifyConn.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM instance_settings;";
                    long count = Convert.ToInt64(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
                    _ = count.Should().Be(1);
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
    #endregion

    private sealed class TestFileDbContextFactory(DbContextOptions<StreamActionsDbContext> options)
        : IDbContextFactory<StreamActionsDbContext>
    {
        public StreamActionsDbContext CreateDbContext() => new(options);
    }
}
