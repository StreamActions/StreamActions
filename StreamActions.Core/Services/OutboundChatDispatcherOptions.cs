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

namespace StreamActions.Core.Services;

/// <summary>
/// Configuration options controlling bounded lane capacities, chunk limits, rate pacing, and deduplication in <see cref="OutboundChatDispatcher"/>.
/// </summary>
public sealed class OutboundChatDispatcherOptions
{
    /// <summary>
    /// Gets or sets the bounded capacity of the <see cref="OutboundChatPriority.ModerationImmediate"/> lane.
    /// </summary>
    public int ModerationLaneCapacity { get; set; } = 256;

    /// <summary>
    /// Gets or sets the bounded capacity of the <see cref="OutboundChatPriority.CommandReply"/> lane.
    /// </summary>
    public int CommandReplyLaneCapacity { get; set; } = 512;

    /// <summary>
    /// Gets or sets the bounded capacity of the <see cref="OutboundChatPriority.EventAnnouncement"/> lane.
    /// </summary>
    public int EventAnnouncementLaneCapacity { get; set; } = 256;

    /// <summary>
    /// Gets or sets the bounded capacity of the <see cref="OutboundChatPriority.Timer"/> lane.
    /// </summary>
    public int TimerLaneCapacity { get; set; } = 64;

    /// <summary>
    /// Gets or sets the default maximum number of word-boundary chunks emitted for a single outbound message.
    /// </summary>
    public int MaxMessageChunks { get; set; } = 2;

    /// <summary>
    /// Gets or sets the window within which identical consecutive messages are suppressed by the duplicate guard.
    /// </summary>
    public TimeSpan DuplicateWindow { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the token-bucket refill window for per-channel chat rate limiting.
    /// </summary>
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the number of messages permitted per <see cref="RateLimitWindow"/> when the bot is a moderator or VIP.
    /// </summary>
    public int ModeratorOrVipMessagesPerWindow { get; set; } = 100;

    /// <summary>
    /// Gets or sets the number of messages permitted per <see cref="RateLimitWindow"/> when the bot is neither a moderator nor a VIP.
    /// </summary>
    public int RegularMessagesPerWindow { get; set; } = 20;
}
