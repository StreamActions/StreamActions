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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Manages discovery, initialization, lookup, and shutdown of <see cref="IPlugin"/> instances.
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Gets the read-only collection of registered plugins.
    /// </summary>
    IReadOnlyCollection<IPlugin> Plugins { get; }

    /// <summary>
    /// Initializes all registered plugins.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask InitializeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully shuts down all initialized plugins.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask ShutdownAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a registered plugin by its unique identifier.
    /// </summary>
    /// <param name="pluginId">The unique plugin identifier.</param>
    /// <returns>The matching <see cref="IPlugin"/>, or <see langword="null"/> if not found.</returns>
    IPlugin? GetPlugin(string pluginId);
}
