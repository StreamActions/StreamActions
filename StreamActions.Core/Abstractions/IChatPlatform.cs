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
/// Defines the abstraction for a chat or streaming platform integration.
/// </summary>
public interface IChatPlatform
{
    /// <summary>
    /// Gets the unique platform identifier (for example, <c>twitch</c> or <c>discord</c>).
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the human-readable platform display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the capabilities supported by this platform integration.
    /// </summary>
    PlatformCapabilities Capabilities { get; }

    /// <summary>
    /// Connects the platform integration for the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous connection operation.</returns>
    ValueTask ConnectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the platform integration for the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous disconnection operation.</returns>
    ValueTask DisconnectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat message to a channel on this platform.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="channelId">The target platform channel identifier.</param>
    /// <param name="message">The message text to send.</param>
    /// <param name="replyToMessageId">An optional message identifier to reply to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous send operation.</returns>
    ValueTask SendMessageAsync(Guid workspaceId, string channelId, string message, string? replyToMessageId = null, CancellationToken cancellationToken = default);
}
