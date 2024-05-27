/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.EventSub;

/// <summary>
/// Represents the transport of a <see cref="EventSubSubscription"/>.
/// </summary>
public sealed record EventSubTransport
{
    /// <summary>
    /// The transport method.
    /// </summary>
    [JsonPropertyName("method")]
    public TransportMethod? Method { get; init; }

    /// <summary>
    /// The callback URL where notifications are sent, if <see cref="Method"/> is set to <see cref="TransportMethod.Webhook"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The URL must use the HTTPS protocol and port 443.
    /// </para>
    /// <para>
    /// NOTE: Redirects are not followed.
    /// </para>
    /// </remarks>
    [JsonPropertyName("callback")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Callback { get; init; }

    /// <summary>
    /// An ID that identifies the conduit to send notifications to, if <see cref="Method"/> is set to <see cref="TransportMethod.Conduit"/>.
    /// </summary>
    [JsonPropertyName("conduit_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ConduitId { get; init; }

    /// <summary>
    /// An ID that identifies the WebSocket that notifications are sent to, if <see cref="Method"/> is set to <see cref="TransportMethod.Websocket"/>.
    /// </summary>
    [JsonPropertyName("session_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; init; }

    /// <summary>
    /// The secret used to verify the signature, if <see cref="Method"/> is set to <see cref="TransportMethod.Webhook"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The secret must be an ASCII string that's a minimum of 10 characters long and a maximum of 100 characters long.
    /// </para>
    /// </remarks>
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
        /// Conduit.
        /// </summary>
        [JsonCustomEnum("conduit")]
        Conduit,
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
