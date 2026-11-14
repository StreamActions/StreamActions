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
/// Defines the lifecycle contract for a StreamActions built-in or external plugin.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Gets the unique plugin identifier (for example, <c>core.commands</c> or <c>twitch.loyalty</c>).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable plugin name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the semantic version of the plugin.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Gets the optional platform identifier required by this plugin, or <see langword="null"/> if universal.
    /// </summary>
    string? RequiredPlatformId => null;

    /// <summary>
    /// Initializes the plugin with the provided host context.
    /// </summary>
    /// <param name="context">The plugin initialization context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous initialization operation.</returns>
    ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully shuts down the plugin and releases any held resources.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous shutdown operation.</returns>
    ValueTask ShutdownAsync(CancellationToken cancellationToken = default);
}
