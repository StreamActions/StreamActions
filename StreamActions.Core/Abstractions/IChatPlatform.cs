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
    /// Gets the maximum character length allowed per single outbound chat message on this platform.
    /// </summary>
    int MaxMessageLength => 500;

    /// <summary>
    /// Gets a value indicating whether the platform permits non-moderator bots to send identical consecutive messages within the duplicate window.
    /// </summary>
    bool SupportsDuplicateMessagesAsNonModerator => false;

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

    /// <summary>
    /// Determines whether the bot account holds moderator or VIP status in the specified channel.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="channelId">The target platform channel identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the bot is a moderator or VIP in the channel; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> IsBotModeratorOrVipAsync(Guid workspaceId, string channelId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    /// <summary>
    /// Sends a private whisper or direct message to a user on this platform.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="targetPlatformUserId">The recipient's platform-specific user identifier.</param>
    /// <param name="message">The whisper message text to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous whisper send operation.</returns>
    ValueTask SendWhisperAsync(Guid workspaceId, string targetPlatformUserId, string message, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
