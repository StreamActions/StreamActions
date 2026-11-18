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
/// Resolves the effective OAuth application credentials for a platform according to the instance OAuth app policy and workspace overrides.
/// </summary>
public interface IOAuthClientConfigResolver
{
    /// <summary>
    /// Resolves the effective OAuth client configuration for the specified platform and optional workspace.
    /// </summary>
    /// <param name="platformId">The platform identifier (for example, <c>twitch</c>).</param>
    /// <param name="workspaceId">The optional workspace identifier for workspace-scoped overrides.</param>
    /// <param name="actor">The optional actor identity used when evaluating permission-gated workspace overrides.</param>
    /// <param name="flow">The optional OAuth flow identifier (for example, <c>login</c>, <c>link</c>, <c>stepup</c>, <c>broadcaster</c>, <c>bot</c>, or <c>instance-bot</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved <see cref="ResolvedOAuthClientConfig"/>, or <see langword="null"/> if no eligible configuration exists.</returns>
    ValueTask<ResolvedOAuthClientConfig?> ResolveAsync(
        string platformId,
        Guid? workspaceId = null,
        PlatformUserIdentity? actor = null,
        string? flow = null,
        CancellationToken cancellationToken = default);
}
