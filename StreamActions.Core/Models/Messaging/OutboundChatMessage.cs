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
/// Represents an immutable outbound chat or whisper message queued for delivery by <see cref="Abstractions.IOutboundChatDispatcher"/>.
/// </summary>
/// <param name="WorkspaceId">The workspace identifier associated with the message.</param>
/// <param name="PlatformId">The origin platform identifier.</param>
/// <param name="ChannelId">The origin platform channel identifier (or empty when sending a whisper).</param>
/// <param name="Message">The outbound message text.</param>
/// <param name="Priority">The priority lane for queuing and dispatch.</param>
/// <param name="TargetScope">The platform routing scope when delivering channel messages.</param>
/// <param name="DuplicatePolicy">The policy controlling 30-second identical-message deduplication.</param>
/// <param name="ReplyToMessageId">An optional inbound message identifier to reply to.</param>
/// <param name="IsWhisper">A value indicating whether the message should be delivered as a private whisper.</param>
/// <param name="TargetPlatformUserId">The recipient platform user identifier when <paramref name="IsWhisper"/> is <see langword="true"/>.</param>
/// <param name="MaxChunks">Optional override for the maximum number of word-boundary message chunks.</param>
public sealed record OutboundChatMessage(
    Guid WorkspaceId,
    string PlatformId,
    string ChannelId,
    string Message,
    OutboundChatPriority Priority = OutboundChatPriority.CommandReply,
    OutboundTargetScope TargetScope = OutboundTargetScope.OriginPlatform,
    DuplicateMessagePolicy DuplicatePolicy = DuplicateMessagePolicy.PlatformDefault,
    string? ReplyToMessageId = null,
    bool IsWhisper = false,
    string? TargetPlatformUserId = null,
    int? MaxChunks = null);
