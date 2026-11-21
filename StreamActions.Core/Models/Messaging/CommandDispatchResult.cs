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

namespace StreamActions.Core.Models.Messaging;

/// <summary>
/// Represents the outcome status of dispatching an inbound command invocation.
/// </summary>
public enum CommandDispatchStatus
{
    /// <summary>
    /// No built-in or custom command matched the trigger.
    /// </summary>
    NotFound = 0,

    /// <summary>
    /// A matching command was found and executed successfully.
    /// </summary>
    Executed = 1,

    /// <summary>
    /// A matching command was found, but it is currently disabled.
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// The invoking user lacked the required permissions or designations.
    /// </summary>
    PermissionDenied = 3,

    /// <summary>
    /// Execution was skipped because the command's global cooldown has not elapsed.
    /// </summary>
    OnGlobalCooldown = 4,

    /// <summary>
    /// Execution was skipped because the invoking user's cooldown has not elapsed.
    /// </summary>
    OnUserCooldown = 5,

    /// <summary>
    /// Execution was skipped because the invoking user had insufficient loyalty points.
    /// </summary>
    InsufficientPoints = 6,

    /// <summary>
    /// Execution was skipped because the workspace stream status did not satisfy the command requirement.
    /// </summary>
    StreamStatusMismatch = 7,

    /// <summary>
    /// Execution was skipped because the originating platform is not permitted for the command.
    /// </summary>
    PlatformNotAllowed = 8,

    /// <summary>
    /// Execution failed due to an unexpected error.
    /// </summary>
    Failed = 9,
}

/// <summary>
/// Represents the result of dispatching an <see cref="Abstractions.ICommandInvocation"/>.
/// </summary>
/// <param name="Status">The dispatch outcome status.</param>
/// <param name="Matched">A value indicating whether a command matched the invocation trigger.</param>
/// <param name="Executed">A value indicating whether the matched command executed.</param>
/// <param name="Trigger">The matched normalized command trigger, if any.</param>
/// <param name="FailureReason">An optional diagnostic or user-facing reason when execution did not succeed.</param>
public sealed record CommandDispatchResult(
    CommandDispatchStatus Status,
    bool Matched,
    bool Executed,
    string? Trigger = null,
    string? FailureReason = null)
{
    /// <summary>
    /// Creates a result representing an unmatched command trigger.
    /// </summary>
    /// <param name="trigger">The unmatched normalized trigger, if available.</param>
    /// <returns>An unmatched <see cref="CommandDispatchResult"/>.</returns>
    public static CommandDispatchResult NotFound(string? trigger = null) =>
        new(CommandDispatchStatus.NotFound, Matched: false, Executed: false, Trigger: trigger);

    /// <summary>
    /// Creates a result representing a successfully executed command.
    /// </summary>
    /// <param name="trigger">The executed normalized trigger.</param>
    /// <returns>An executed <see cref="CommandDispatchResult"/>.</returns>
    public static CommandDispatchResult Success(string trigger) =>
        new(CommandDispatchStatus.Executed, Matched: true, Executed: true, Trigger: trigger);

    /// <summary>
    /// Creates a result representing a matched command whose execution was skipped or failed.
    /// </summary>
    /// <param name="status">The rejection or failure status.</param>
    /// <param name="trigger">The matched normalized trigger.</param>
    /// <param name="failureReason">An optional reason explaining why the command did not execute.</param>
    /// <returns>A rejected <see cref="CommandDispatchResult"/>.</returns>
    public static CommandDispatchResult Rejected(CommandDispatchStatus status, string trigger, string? failureReason = null) =>
        new(status, Matched: true, Executed: false, Trigger: trigger, FailureReason: failureReason);
}
