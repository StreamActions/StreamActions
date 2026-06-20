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

using StreamActions.Common.Json.Serialization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Common;

/// <summary>
/// Metadata for an EventSub event.
/// </summary>
public sealed record EventMetadata
{
    /// <summary>
    /// An ID that uniquely identifies the message.
    /// </summary>
    [JsonPropertyName("message_id")]
    public Guid? MessageId { get; init; }

    /// <summary>
    /// The type of message.
    /// </summary>
    [JsonPropertyName("message_type")]
    public MessageTypes? MessageType { get; init; }

    /// <summary>
    /// The timestamp of when the message was sent.
    /// </summary>
    [JsonPropertyName("message_timestamp")]
    public DateTime? MessageTimestamp { get; init; }

    /// <summary>
    /// The subscription type that the message is associated with.
    /// </summary>
    /// <remarks>
    /// This field is only included in <see cref="MessageTypes.Notification"/> and <see cref="MessageTypes.Revocation"/> messages.
    /// </remarks>
    [JsonPropertyName("subscription_type")]
    public string? SubscriptionType { get; init; }

    /// <summary>
    /// The subscription version that the message is associated with.
    /// </summary>
    /// <remarks>
    /// This field is only included in <see cref="MessageTypes.Notification"/> and <see cref="MessageTypes.Revocation"/> messages.
    /// </remarks>
    [JsonPropertyName("subscription_version")]
    public string? SubscriptionVersion { get; init; }

    /// <summary>
    /// The type of the message.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<MessageTypes>))]
    public enum MessageTypes
    {
        /// <summary>
        /// The first message that the EventSub WebSocket server sends after your client connects to the server.
        /// </summary>
        [JsonCustomEnum("session_welcome")]
        SessionWelcome,
        /// <summary>
        /// A message that the EventSub WebSocket server sends your client to indicate that the WebSocket connection is healthy.
        /// </summary>
        [JsonCustomEnum("session_keepalive")]
        SessionKeepAlive,
        /// <summary>
        /// A message that the EventSub WebSocket server sends your client when an event that you subscribe to occurs.
        /// </summary>
        [JsonCustomEnum("notification")]
        Notification,
        /// <summary>
        /// A message that the EventSub WebSocket server sends if the user no longer exists or they revoked the authorization token that the subscription relied on.
        /// </summary>
        [JsonCustomEnum("revocation")]
        Revocation,
        /// <summary>
        /// A message that the EventSub WebSocket server sends if the server must drop the connection.
        /// </summary>
        [JsonCustomEnum("session_reconnect")]
        SessionReconnect
    }
}
