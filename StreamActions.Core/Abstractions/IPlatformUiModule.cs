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
/// Describes a navigation item contributed to the web UI by a platform module.
/// </summary>
/// <param name="Title">The display title of the navigation item.</param>
/// <param name="Route">The relative route path for the navigation item.</param>
/// <param name="Icon">An optional icon identifier.</param>
/// <param name="RequiredPermission">An optional permission key required to view this item.</param>
public sealed record PlatformNavItem(string Title, string Route, string? Icon = null, string? RequiredPermission = null);

/// <summary>
/// Defines UI navigation metadata contributed by a platform integration.
/// </summary>
public interface IPlatformUiModule
{
    /// <summary>
    /// Gets the platform identifier associated with this UI module.
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the human-readable display name for the platform UI section.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the navigation items contributed by this platform module.
    /// </summary>
    IReadOnlyList<PlatformNavItem> NavItems { get; }
}
