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

using StreamActions.Core.Models.Entities;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Event arguments raised when <see cref="InstanceSettings"/> are updated.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InstanceSettingsChangedEventArgs"/> class.
/// </remarks>
/// <param name="settings">The updated instance settings.</param>
public sealed class InstanceSettingsChangedEventArgs(InstanceSettings settings) : EventArgs
{

    /// <summary>
    /// Gets the updated instance settings.
    /// </summary>
    public InstanceSettings Settings { get; } = settings;
}

/// <summary>
/// Manages database-persisted <see cref="InstanceSettings"/> with hot-reload change notifications.
/// </summary>
public interface IInstanceSettingsService
{
    /// <summary>
    /// Occurs when <see cref="InstanceSettings"/> are updated at runtime.
    /// </summary>
    event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged;

    /// <summary>
    /// Retrieves the current <see cref="InstanceSettings"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The current <see cref="InstanceSettings"/>.</returns>
    ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists updated <see cref="InstanceSettings"/> and notifies subscribers.
    /// </summary>
    /// <param name="settings">The updated settings to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous update operation.</returns>
    ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default);
}
