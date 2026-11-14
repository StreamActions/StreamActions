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

using Microsoft.Extensions.Diagnostics.HealthChecks;
using StreamActions.Core.Abstractions;

namespace StreamActions.Web.Health;

/// <summary>
/// Health check verifying that <see cref="IPluginManager"/> is available and ready.
/// </summary>
public sealed class PluginReadinessHealthCheck : IHealthCheck
{
    /// <summary>
    /// The plugin manager inspected for readiness.
    /// </summary>
    private readonly IPluginManager _pluginManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginReadinessHealthCheck"/> class.
    /// </summary>
    /// <param name="pluginManager">The plugin manager to inspect.</param>
    public PluginReadinessHealthCheck(IPluginManager pluginManager)
    {
        ArgumentNullException.ThrowIfNull(pluginManager);
        this._pluginManager = pluginManager;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        int pluginCount = this._pluginManager.Plugins.Count;
        return Task.FromResult(HealthCheckResult.Healthy($"Plugin manager is ready ({pluginCount} plugin(s) registered)."));
    }
}
