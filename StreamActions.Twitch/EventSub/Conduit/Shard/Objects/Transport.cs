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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Conduit.Shard.Objects;

/// <summary>
/// The disabled transport.
/// </summary>
public sealed record Transport
{
    /// <summary>
    /// The transport method (e.g. websocket or webhook).
    /// </summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>
    /// Optional. Webhook callback URL.
    /// </summary>
    [JsonPropertyName("callback")]
    public string? Callback { get; init; }

    /// <summary>
    /// Optional. WebSocket session ID.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    /// Optional. Time that the WebSocket session connected.
    /// </summary>
    [JsonPropertyName("connected_at")]
    public DateTime? ConnectedAt { get; init; }

    /// <summary>
    /// Optional. Time that the WebSocket session disconnected.
    /// </summary>
    [JsonPropertyName("disconnected_at")]
    public DateTime? DisconnectedAt { get; init; }
}
