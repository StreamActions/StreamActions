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

using StreamActions.Core.Models.Metadata;

namespace StreamActions.Core.Models.Entities;

/// <summary>
/// Represents a custom chat or slash command configured within a <see cref="Workspace"/>.
/// </summary>
public sealed class CustomCommand
{
    /// <summary>
    /// Gets or sets the unique command identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the primary command trigger.
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized JSON array of command aliases.
    /// </summary>
    public string AliasesJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the global cooldown in seconds.
    /// </summary>
    public int GlobalCooldownSeconds { get; set; }

    /// <summary>
    /// Gets or sets the per-user cooldown in seconds.
    /// </summary>
    public int UserCooldownSeconds { get; set; }

    /// <summary>
    /// Gets or sets the loyalty points cost to invoke the command.
    /// </summary>
    public long CostPoints { get; set; }

    /// <summary>
    /// Gets or sets the loyalty points rewarded upon invocation.
    /// </summary>
    public long RewardPoints { get; set; }

    /// <summary>
    /// Gets or sets the optional counter name bound to this command.
    /// </summary>
    public string? BoundCounterName { get; set; }

    /// <summary>
    /// Gets or sets how the bound counter is affected when the command is used.
    /// </summary>
    public CounterUsageBehavior CounterUsageBehavior { get; set; }

    /// <summary>
    /// Gets or sets how the command response is delivered.
    /// </summary>
    public CommandDeliveryMode DeliveryMode { get; set; }

    /// <summary>
    /// Gets or sets the stream status requirement for command execution.
    /// </summary>
    public StreamStatusRequirement StreamStatusRequirement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command is hidden from the public viewer portal.
    /// </summary>
    public bool IsHiddenFromPublicPortal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command is exposed as a Discord slash command.
    /// </summary>
    public bool ExposeAsDiscordSlashCommand { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON action routine executed by this command.
    /// </summary>
    public string RoutineJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the command is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Tier A JSON metadata for platform-specific overrides.
    /// </summary>
    public CommandMetadata Metadata { get; set; } = new();
}
