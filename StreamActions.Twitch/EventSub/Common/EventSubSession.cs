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
/// Session data for an EventSub websocket connection.
/// </summary>
public sealed record EventSubSession
{
    /// <summary>
    /// An ID that uniquely identifies this WebSocket connection.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The connection status.
    /// </summary>
    [JsonPropertyName("status")]
    public ConnectionStatus? Status { get; init; }

    /// <summary>
    /// The maximum number of seconds that you should expect silence before receiving a <see cref="EventMetadata.MessageTypes.SessionKeepAlive"/> message.
    /// This is also the time limit to enable at least one subscription on this session.
    /// </summary>
    [JsonPropertyName("keepalive_timeout_seconds")]
    public int? KeepaliveTimeoutSeconds { get; init; }

    /// <summary>
    /// The URL to use for reconnecting to the WebSocket.
    /// </summary>
    /// <remarks>
    /// This field is only included in <see cref="EventMetadata.MessageTypes.SessionReconnect"/> messages.
    /// </remarks>
    [JsonPropertyName("reconnect_url")]
    public Uri? ReconnectUrl { get; init; }

    /// <summary>
    /// The timestamp of when the WebSocket connection was established.
    /// </summary>
    [JsonPropertyName("connected_at")]
    public DateTime? ConnectedAt { get; init; }

    /// <summary>
    /// The connection status.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<ConnectionStatus>))]
    public enum ConnectionStatus
    {
        /// <summary>
        /// The session is connected.
        /// </summary>
        [JsonCustomEnum("connected")]
        Connected,
        /// <summary>
        /// The session is reconnecting.
        /// </summary>
        [JsonCustomEnum("reconnecting")]
        Reconnecting
    }
}
