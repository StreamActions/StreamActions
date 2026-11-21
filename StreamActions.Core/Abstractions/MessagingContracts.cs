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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Represents a platform chat channel belonging to a workspace.
/// </summary>
public interface IChatChannel
{
    /// <summary>
    /// Gets the workspace identifier that owns this channel.
    /// </summary>
    Guid WorkspaceId { get; }

    /// <summary>
    /// Gets the platform identifier (for example, <c>twitch</c> or <c>discord</c>).
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the platform-specific channel identifier.
    /// </summary>
    string ChannelId { get; }

    /// <summary>
    /// Gets the human-readable channel name or login.
    /// </summary>
    string ChannelName { get; }
}

/// <summary>
/// Represents the common envelope metadata shared by all inbound chat messages, whispers, and command invocations.
/// </summary>
public interface IMessageEnvelope
{
    /// <summary>
    /// Gets the workspace identifier associated with the message.
    /// </summary>
    Guid WorkspaceId { get; }

    /// <summary>
    /// Gets the platform identifier where the message originated.
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the platform-specific unique message identifier.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Gets the chat user who sent the message.
    /// </summary>
    IChatUser Sender { get; }

    /// <summary>
    /// Gets the raw unparsed message text.
    /// </summary>
    string RawText { get; }

    /// <summary>
    /// Gets the UTC timestamp when the message was received.
    /// </summary>
    DateTimeOffset ReceivedAtUtc { get; }
}

/// <summary>
/// Represents an inbound public or channel-scoped chat message.
/// </summary>
public interface IChatMessage : IMessageEnvelope
{
    /// <summary>
    /// Gets the channel in which the message was sent.
    /// </summary>
    IChatChannel Channel { get; }

    /// <summary>
    /// Gets the optional parent message identifier if this message is a threaded reply.
    /// </summary>
    string? ReplyToMessageId { get; }

    /// <summary>
    /// Gets a value indicating whether the message was sent as an action (for example, <c>/me</c>).
    /// </summary>
    bool IsAction { get; }

    /// <summary>
    /// Gets platform-specific read-only metadata tags attached to the message.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}

/// <summary>
/// Represents an inbound private whisper or direct message.
/// </summary>
public interface IWhisper : IMessageEnvelope
{
    /// <summary>
    /// Gets the optional recipient platform user identifier when available.
    /// </summary>
    string? TargetPlatformUserId { get; }
}

/// <summary>
/// Represents a parsed command invocation extracted from an inbound message.
/// </summary>
public interface ICommandInvocation : IMessageEnvelope
{
    /// <summary>
    /// Gets the matched command prefix (for example, <c>!</c>), or empty for prefixless/slash invocations.
    /// </summary>
    string Prefix { get; }

    /// <summary>
    /// Gets the normalized lowercase command trigger without prefix.
    /// </summary>
    string Trigger { get; }

    /// <summary>
    /// Gets the parsed command arguments, with quoted arguments unquoted into individual entries.
    /// </summary>
    IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the raw unparsed argument string following the trigger, trimmed of leading and trailing whitespace.
    /// </summary>
    string RawArguments { get; }
}

/// <summary>
/// Represents a command invocation originating from a channel chat message.
/// </summary>
public interface IChatCommand : IChatMessage, ICommandInvocation
{
}

/// <summary>
/// Represents a command invocation originating from a private whisper or direct message.
/// </summary>
public interface IWhisperCommand : IWhisper, ICommandInvocation
{
}
