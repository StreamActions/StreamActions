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

using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace StreamActions.Database.Interceptors;

/// <summary>
/// Configures SQLite connection pragmas (WAL mode, synchronous NORMAL, foreign keys, and busy timeout) whenever a connection opens.
/// </summary>
public sealed class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    /// <summary>
    /// SQLite connection initialization pragmas applied whenever a connection opens.
    /// </summary>
    private const string _pragmaSql =
        "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";

    /// <inheritdoc/>
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using DbCommand command = connection.CreateCommand();
        command.CommandText = _pragmaSql;
        _ = command.ExecuteNonQuery();

        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc/>
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        DbCommand command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = _pragmaSql;
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }
}
