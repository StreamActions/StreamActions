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

namespace StreamActions.Core.Commands;

/// <summary>
/// Defines the execution contract for a built-in StreamActions command or <c>!bot</c> sub-command handler.
/// </summary>
public interface IBuiltInCommandHandler
{
    /// <summary>
    /// Shared empty top-level trigger list returned by default when a handler does not expose top-level aliases.
    /// </summary>
    private static readonly IReadOnlyList<string> _emptyTriggers = [];

    /// <summary>
    /// Gets the canonical command identifier (for example, <c>bot.command</c>).
    /// </summary>
    string CommandId { get; }

    /// <summary>
    /// Gets the default sub-command trigger under <c>!bot</c> or <c>!&lt;botname&gt;</c> (for example, <c>command</c>).
    /// </summary>
    string DefaultSubCommandTrigger { get; }

    /// <summary>
    /// Gets optional top-level triggers that are exposed directly without the <c>!bot</c> prefix by default.
    /// </summary>
    IReadOnlyList<string> OptionalTopLevelTriggers => _emptyTriggers;

    /// <summary>
    /// Gets a value indicating whether an enabled workspace custom command with a matching trigger takes precedence over this built-in command when invoked at top level.
    /// </summary>
    bool AllowCustomOverride => false;

    /// <summary>
    /// Gets the optional permission key required to execute this built-in command, or <see langword="null"/> if unrestricted.
    /// </summary>
    string? RequiredPermissionKey => null;

    /// <summary>
    /// Gets the global cooldown in seconds between invocations of this command.
    /// </summary>
    int GlobalCooldownSeconds => 0;

    /// <summary>
    /// Gets the per-user cooldown in seconds between invocations of this command.
    /// </summary>
    int UserCooldownSeconds => 0;

    /// <summary>
    /// Executes the built-in command using the supplied execution <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The built-in command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous command execution.</returns>
    ValueTask ExecuteAsync(BuiltInCommandContext context, CancellationToken cancellationToken = default);
}
