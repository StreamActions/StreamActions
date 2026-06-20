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

using StreamActions.Common.Attributes;
using StreamActions.Twitch.EventSub.Common;

namespace StreamActions.Twitch.EventSub;

/// <summary>
/// Handles processing of messages for EventSub, including creating websocket connections.
/// </summary>
[ETag("EventSub Subscription Types", 57,
    "https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/", "0", "2024-06-02T20:12Z",
    "TwitchEventSubSubscriptionTypesParser", [])]
[ETag("EventSub Fields", 57,
    "https://dev.twitch.tv/docs/eventsub/eventsub-reference/", "0", "2024-06-02T20:12Z",
    "TwitchEventSubReferenceParser", [])]
public sealed class EventSub
{
    /// <summary>
    /// Close codes for EventSub WebSocket connections.
    /// </summary>
    public enum CloseCodes
    {
        /// <summary>
        /// Unknown close reason.
        /// </summary>
        None = 0,
        /// <summary>
        /// The connection was closed normally.
        /// </summary>
        NormalClosure = 1000,
        /// <summary>
        /// Indicates a problem with the server (similar to an HTTP 500 status code).
        /// </summary>
        InternalServerError = 4000,
        /// <summary>
        /// Sending outgoing messages to the server is prohibited with the exception of pong messages.
        /// </summary>
        ClientSentInboundTraffic = 4001,
        /// <summary>
        /// You must respond to ping messages with a pong message.
        /// </summary>
        ClientFailedPingPong = 4002,
        /// <summary>
        /// When you connect to the server, you must create a subscription within <see cref="EventSubSession.KeepAliveInterval"/> seconds or the connection is closed.
        /// </summary>
        ConnectionUnused = 4003,
        /// <summary>
        /// When you receive a <see cref="EventMetadata.MessageTypes.SessionReconnect"/> message, you have 30 seconds to reconnect to the server and close the old connection.
        /// </summary>
        ReconnectGraceTimeExpired = 4004,
        /// <summary>
        /// Transient network timeout.
        /// </summary>
        NetworkTimeout = 4005,
        /// <summary>
        /// Transient network error.
        /// </summary>
        NetworkError = 4006,
        /// <summary>
        /// The reconnect URL is invalid.
        /// </summary>
        InvalidReconnect = 4007
    }
}
