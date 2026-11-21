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
/// Immutable concrete representation of an <see cref="IChatCommand"/>.
/// </summary>
/// <param name="WorkspaceId">The workspace identifier associated with the command.</param>
/// <param name="PlatformId">The platform identifier where the command originated.</param>
/// <param name="MessageId">The platform-specific unique message identifier.</param>
/// <param name="Sender">The chat user who invoked the command.</param>
/// <param name="Channel">The channel in which the command was invoked.</param>
/// <param name="RawText">The raw unparsed message text.</param>
/// <param name="ReceivedAtUtc">The UTC timestamp when the message was received.</param>
/// <param name="Prefix">The matched command prefix.</param>
/// <param name="Trigger">The normalized lowercase command trigger.</param>
/// <param name="Arguments">The parsed command arguments.</param>
/// <param name="RawArguments">The raw trimmed argument text following the trigger.</param>
/// <param name="ReplyToMessageId">The optional parent message identifier if the invoking message was a threaded reply.</param>
/// <param name="IsAction">A value indicating whether the invoking message was sent as an action.</param>
/// <param name="Metadata">Optional platform-specific metadata tags attached to the invoking message.</param>
public sealed record ChatCommand(
    Guid WorkspaceId,
    string PlatformId,
    string MessageId,
    IChatUser Sender,
    IChatChannel Channel,
    string RawText,
    DateTimeOffset ReceivedAtUtc,
    string Prefix,
    string Trigger,
    IReadOnlyList<string> Arguments,
    string RawArguments,
    string? ReplyToMessageId = null,
    bool IsAction = false,
    IReadOnlyDictionary<string, string>? Metadata = null) : IChatCommand
{
    /// <summary>
    /// Shared empty metadata dictionary used when no metadata is supplied.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _emptyMetadata =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets platform-specific read-only metadata tags attached to the invoking message.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = Metadata ?? _emptyMetadata;
}
