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

using StreamActions.Core.Models;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Resolves the effective chat bot credentials for a workspace platform according to <see cref="InstanceBotAccountPolicy"/> and workspace settings.
/// </summary>
public interface IBotCredentialResolver
{
    /// <summary>
    /// Resolves the effective bot chat credential for the specified workspace and platform.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier (for example, <c>twitch</c>).</param>
    /// <param name="actor">The optional actor identity used when evaluating permission-gated custom bot overrides.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="BotCredentialResolutionResult"/> describing the resolved token or required authorization state.</returns>
    ValueTask<BotCredentialResolutionResult> ResolveAsync(
        Guid workspaceId,
        string platformId,
        PlatformUserIdentity? actor = null,
        CancellationToken cancellationToken = default);
}
