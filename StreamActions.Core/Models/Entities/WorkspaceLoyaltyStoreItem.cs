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
/// Represents a loyalty points store or soundboard item within a <see cref="Workspace"/>.
/// </summary>
public sealed class WorkspaceLoyaltyStoreItem
{
    /// <summary>
    /// Gets or sets the unique store item identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the unique item slug within the workspace.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display title of the store item.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the loyalty points cost to redeem this item.
    /// </summary>
    public long CostPoints { get; set; }

    /// <summary>
    /// Gets or sets the global cooldown in seconds between redemptions.
    /// </summary>
    public int GlobalCooldownSeconds { get; set; }

    /// <summary>
    /// Gets or sets the per-user cooldown in seconds between redemptions.
    /// </summary>
    public int UserCooldownSeconds { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum redemptions allowed per stream.
    /// </summary>
    public int? MaxRedemptionsPerStream { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON action routine executed upon redemption.
    /// </summary>
    public string RoutineJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the store item is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}
