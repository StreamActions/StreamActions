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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StreamActions.Database;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Readiness health check verifying database connectivity through <see cref="IDbContextFactory{TContext}"/>.
/// </summary>
internal sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    /// <summary>
    /// Database context factory used to verify connectivity.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _contextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseReadinessHealthCheck"/> class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    public DatabaseReadinessHealthCheck(IDbContextFactory<StreamActionsDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        this._contextFactory = contextFactory;
    }

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Health checks must report database connectivity failures as Unhealthy rather than throwing.")]
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
                return canConnect
                    ? HealthCheckResult.Healthy("Database connection is ready.")
                    : HealthCheckResult.Unhealthy("Database connection check returned false.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Database connection check threw an exception.", ex);
        }
    }
}
