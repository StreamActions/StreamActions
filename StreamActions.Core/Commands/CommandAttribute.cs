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

using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Commands;

/// <summary>
/// Marks a built-in command handler class or method and declares its default trigger, aliases, permission key, cooldowns, and custom override policy.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandAttribute"/> class.
    /// </summary>
    /// <param name="trigger">The primary command trigger without prefix (normalized to lowercase invariant).</param>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes command triggers to lowercase invariant.")]
    public CommandAttribute(string trigger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        this.Trigger = trigger.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the primary normalized lowercase command trigger without prefix.
    /// </summary>
    public string Trigger { get; }

    /// <summary>
    /// Gets or sets optional alternative trigger aliases for this command.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Attribute named arguments require array types for collection metadata.")]
    public string[] Aliases { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional human-readable description of what the command does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the default permission key required to invoke this command (for example, <c>action.run_command</c>).
    /// </summary>
    public string? DefaultPermissionKey { get; set; }

    /// <summary>
    /// Gets or sets the default global cooldown in seconds between command invocations.
    /// </summary>
    public int GlobalCooldownSeconds { get; set; }

    /// <summary>
    /// Gets or sets the default per-user cooldown in seconds between command invocations.
    /// </summary>
    public int UserCooldownSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether workspace custom commands are permitted to override this built-in command.
    /// </summary>
    public bool AllowCustomOverride { get; set; }
}
