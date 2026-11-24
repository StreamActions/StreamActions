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

using StreamActions.Twitch.Api.Common;

namespace StreamActions.Scripting.Runtime;

/// <summary>
/// Disposable lease providing a workspace-scoped <see cref="TwitchSession"/> to <see cref="Proxies.BoundNamespaceProxy"/> without coupling <c>StreamActions.Scripting</c> to <c>StreamActions.Twitch.UI</c>.
/// </summary>
public interface ITwitchSessionLease : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the workspace-scoped <see cref="TwitchSession"/>.
    /// </summary>
    TwitchSession Session { get; }
}

/// <summary>
/// Resolves an authenticated <see cref="ITwitchSessionLease"/> for a workspace.
/// </summary>
public interface ITwitchSessionLeaseProvider
{
    /// <summary>
    /// Acquires a disposable <see cref="ITwitchSessionLease"/> for <paramref name="workspaceId"/>, or <see langword="null"/> if not configured.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A disposable session lease, or <see langword="null"/>.</returns>
    ValueTask<ITwitchSessionLease?> AcquireSessionAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}

