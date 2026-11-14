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

namespace StreamActions.Core.Models.Entities;

/// <summary>
/// Represents an isolated bot workspace owned by a <see cref="PanelUser"/>.
/// </summary>
public sealed class Workspace
{
    /// <summary>
    /// Gets or sets the unique workspace identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the <see cref="PanelUser"/> who owns this workspace.
    /// </summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>
    /// Gets or sets the workspace display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the primary platform anchoring this workspace.
    /// </summary>
    public string PrimaryPlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this workspace may override the instance default bot account.
    /// </summary>
    public bool AllowCustomBotAccountOverride { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the workspace was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}
