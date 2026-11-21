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
/// Describes a registered <see cref="IBuiltInCommandHandler"/> with effective metadata merged from <see cref="CommandAttribute"/>.
/// </summary>
/// <param name="Handler">The registered built-in command handler.</param>
/// <param name="CommandId">The normalized canonical command identifier.</param>
/// <param name="DefaultSubCommandTrigger">The normalized default <c>!bot</c> sub-command trigger.</param>
/// <param name="SubCommandTriggers">All normalized sub-command triggers recognized for this handler.</param>
/// <param name="OptionalTopLevelTriggers">Normalized top-level triggers declared by the handler.</param>
/// <param name="AllTopLevelCandidateTriggers">All normalized triggers exposed at top level when top-level aliases are enabled for a workspace.</param>
/// <param name="AllowCustomOverride">A value indicating whether enabled workspace custom commands override top-level invocations of this command.</param>
/// <param name="RequiredPermissionKey">The optional permission key required to run this command.</param>
/// <param name="GlobalCooldownSeconds">The effective global cooldown in seconds.</param>
/// <param name="UserCooldownSeconds">The effective per-user cooldown in seconds.</param>
/// <param name="Description">Optional human-readable command description.</param>
public sealed record BuiltInCommandRegistration(
    IBuiltInCommandHandler Handler,
    string CommandId,
    string DefaultSubCommandTrigger,
    IReadOnlyList<string> SubCommandTriggers,
    IReadOnlyList<string> OptionalTopLevelTriggers,
    IReadOnlyList<string> AllTopLevelCandidateTriggers,
    bool AllowCustomOverride,
    string? RequiredPermissionKey,
    int GlobalCooldownSeconds,
    int UserCooldownSeconds,
    string? Description = null);

/// <summary>
/// Discovers, stores, and resolves built-in <see cref="IBuiltInCommandHandler"/> instances and workspace top-level alias exposure settings.
/// </summary>
public interface IBuiltInCommandRegistry
{
    /// <summary>
    /// Registers or updates a built-in command handler and merges any <see cref="CommandAttribute"/> metadata on its type.
    /// </summary>
    /// <param name="handler">The built-in command handler to register.</param>
    void Register(IBuiltInCommandHandler handler);

    /// <summary>
    /// Resolves a registered built-in command by its canonical <paramref name="commandId"/>.
    /// </summary>
    /// <param name="commandId">The canonical command identifier (for example, <c>bot.command</c>).</param>
    /// <returns>The matching <see cref="BuiltInCommandRegistration"/>, or <see langword="null"/> if not found.</returns>
    BuiltInCommandRegistration? ResolveByCommandId(string commandId);

    /// <summary>
    /// Resolves a registered built-in command by its <c>!bot</c> sub-command trigger or alias.
    /// </summary>
    /// <param name="subCommandTrigger">The sub-command trigger without prefix.</param>
    /// <returns>The matching <see cref="BuiltInCommandRegistration"/>, or <see langword="null"/> if not found.</returns>
    BuiltInCommandRegistration? ResolveBySubCommandTrigger(string subCommandTrigger);

    /// <summary>
    /// Resolves a built-in command eligible for top-level invocation in <paramref name="workspaceId"/>, honoring workspace top-level alias exposure settings and localized command triggers.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="trigger">The normalized command trigger without prefix.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="BuiltInCommandRegistration"/> if top-level invocation is active; otherwise, <see langword="null"/>.</returns>
    ValueTask<BuiltInCommandRegistration?> ResolveByTopLevelTriggerAsync(
        Guid workspaceId,
        string trigger,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables top-level alias exposure for a built-in <paramref name="commandId"/> (or <c>__all__</c>) within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="commandId">The canonical command identifier, or <see cref="BuiltInCommandRegistry.AllCommandsAliasKey"/> for workspace-wide default.</param>
    /// <param name="enabled"><see langword="true"/> to expose top-level aliases; <see langword="false"/> to disable top-level aliases.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SetTopLevelAliasEnabledAsync(
        Guid workspaceId,
        string commandId,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether top-level alias exposure is enabled for <paramref name="commandId"/> in <paramref name="workspaceId"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="commandId">The canonical command identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if top-level aliases are enabled; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> IsTopLevelAliasEnabledAsync(
        Guid workspaceId,
        string commandId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all currently registered built-in command registrations.
    /// </summary>
    /// <returns>A read-only collection of <see cref="BuiltInCommandRegistration"/> entries.</returns>
    IReadOnlyCollection<BuiltInCommandRegistration> GetAll();
}
