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

using StreamActions.Core.Abstractions;

namespace StreamActions.Core.Models.Messaging;

/// <summary>
/// Immutable concrete representation of an <see cref="IWhisper"/>.
/// </summary>
/// <param name="WorkspaceId">The workspace identifier associated with the whisper.</param>
/// <param name="PlatformId">The platform identifier where the whisper originated.</param>
/// <param name="MessageId">The platform-specific unique message identifier.</param>
/// <param name="Sender">The chat user who sent the whisper.</param>
/// <param name="RawText">The raw unparsed whisper message text.</param>
/// <param name="ReceivedAtUtc">The UTC timestamp when the whisper was received.</param>
/// <param name="TargetPlatformUserId">The optional recipient platform user identifier.</param>
public sealed record WhisperMessage(
    Guid WorkspaceId,
    string PlatformId,
    string MessageId,
    IChatUser Sender,
    string RawText,
    DateTimeOffset ReceivedAtUtc,
    string? TargetPlatformUserId = null) : IWhisper;
