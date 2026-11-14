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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Interceptors;

namespace StreamActions.Database.Tests;

internal sealed class TestSqliteContextFactory : IDbContextFactory<StreamActionsDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<StreamActionsDbContext> _options;

    public TestSqliteContextFactory()
    {
        this._connection = new SqliteConnection("Data Source=:memory:");
        this._connection.Open();

        DbContextOptionsBuilder<StreamActionsDbContext> builder = new();
        _ = StreamActionsDbContext.ConfigureDefaultOptions(builder)
            .UseSqlite(this._connection)
            .AddInterceptors(new SqliteConnectionInterceptor());
        this._options = builder.Options;

        using StreamActionsDbContext dbContext = this.CreateDbContext();
        _ = dbContext.Database.EnsureCreated();
    }

    public StreamActionsDbContext CreateDbContext() => new(this._options);

    public async Task<(Guid UserId, Guid WorkspaceId)> SeedWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = Guid.CreateVersion7();
        Guid workspaceId = Guid.CreateVersion7();

        StreamActionsDbContext dbContext = this.CreateDbContext();
        await using (dbContext.ConfigureAwait(false))
        {
            _ = dbContext.PanelUsers.Add(new PanelUser
            {
                Id = userId,
                DisplayName = "Test Broadcaster",
                IsRoot = true,
                IsGlobalAdmin = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });

            _ = dbContext.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                OwnerUserId = userId,
                Name = "Test Workspace",
                PrimaryPlatformId = "twitch",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });

            _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return (userId, workspaceId);
    }

    public void Dispose() => this._connection.Dispose();
}
