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
using System.Data.Common;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class SqliteConnectionInterceptorTests
{
    #region ConnectionOpened
    [Fact]
    [Trait("Member", "ConnectionOpened")]
    public async Task ConnectionOpened_EnablesForeignKeysAndBusyTimeout()
    {
        DbContextOptions<StreamActionsDbContext> options = new DbContextOptionsBuilder<StreamActionsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .AddInterceptors(new Interceptors.SqliteConnectionInterceptor())
            .Options;

        StreamActionsDbContext context = new(options);
        await using (context.ConfigureAwait(false))
        {
            await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

            DbConnection connection = context.Database.GetDbConnection();
            await using DbCommand fkCommand = connection.CreateCommand();
            fkCommand.CommandText = "PRAGMA foreign_keys;";
            object? fkResult = await fkCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken);

            await using DbCommand timeoutCommand = connection.CreateCommand();
            timeoutCommand.CommandText = "PRAGMA busy_timeout;";
            object? timeoutResult = await timeoutCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken);

            _ = Convert.ToInt64(fkResult, System.Globalization.CultureInfo.InvariantCulture).Should().Be(1);
            _ = Convert.ToInt64(timeoutResult, System.Globalization.CultureInfo.InvariantCulture).Should().Be(5000);
        }
    }
    #endregion
}
