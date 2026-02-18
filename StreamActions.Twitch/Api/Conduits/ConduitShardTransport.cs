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

namespace StreamActions.Twitch.Api.Conduits;

/// <summary>
/// Represents the transport details for a conduit shard.
/// </summary>
public sealed record ConduitShardTransport
{
    /// <summary>
    /// The transport method.
    /// </summary>
    [JsonPropertyName("method")]
    public TransportMethod? Method { get; init; }

    /// <summary>
    /// The callback URL where notifications are sent, if <see cref="Method"/> is set to <see cref="TransportMethod.Webhook"/>.
    /// </summary>
    [JsonPropertyName("callback")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Callback { get; init; }

    /// <summary>
    /// An ID that identifies the WebSocket that notifications are sent to, if <see cref="Method"/> is set to <see cref="TransportMethod.Websocket"/>.
    /// </summary>
    [JsonPropertyName("session_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; init; }

    /// <summary>
    /// The secret used to verify the signature, if <see cref="Method"/> is set to <see cref="TransportMethod.Webhook"/>.
    /// </summary>
    [JsonPropertyName("secret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Secret { get; init; }

    /// <summary>
    /// The date and time when the WebSocket connection was established, if <see cref="Method"/> is set to <see cref="TransportMethod.Websocket"/>.
    /// </summary>
    [JsonPropertyName("connected_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ConnectedAt { get; init; }

    /// <summary>
    /// The date and time when the WebSocket connection was lost, if <see cref="Method"/> is set to <see cref="TransportMethod.Websocket"/>.
    /// </summary>
    [JsonPropertyName("disconnected_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? DisconnectedAt { get; init; }

    /// <summary>
    /// The transport method.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<TransportMethod>))]
    public enum TransportMethod
    {
        /// <summary>
        /// Webhook transport.
        /// </summary>
        [JsonCustomEnum("webhook")]
        Webhook,
        /// <summary>
        /// Websocket transport.
        /// </summary>
        [JsonCustomEnum("websocket")]
        Websocket
    }
}
