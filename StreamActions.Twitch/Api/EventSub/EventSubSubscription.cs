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
using StreamActions.Twitch.Api.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.EventSub;

public sealed record EventSubSubscription
{
    /// <summary>
    /// An ID that identifies the subscription.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

    /// <summary>
    /// The subscription's status. The subscriber receives events only for <see cref="SubscriptionStatus.Enabled"/> subscriptions.
    /// </summary>
    [JsonPropertyName("status")]
    public SubscriptionStatus? Status { get; init; }

    /// <summary>
    /// The subscription's type.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// The version number that identifies this definition of the subscription's data.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// EventSUb Subscription Status.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<SubscriptionStatus>))]
    public enum SubscriptionStatus
    {
        /// <summary>
        /// The subscription is enabled.
        /// </summary>
        [JsonCustomEnum("enabled")]
        Enabled,
        /// <summary>
        /// The subscription is pending verification of the specified callback URL.
        /// </summary>
        [JsonCustomEnum("webhook_callback_verification_pending")]
        WebhookCallbackVerificationPending,
        /// <summary>
        /// The specified callback URL failed verification.
        /// </summary>
        [JsonCustomEnum("webhook_callback_verification_failed")]
        WebhookCallbackVerificationFailed,
        /// <summary>
        /// The notification delivery failure rate was too high.
        /// </summary>
        [JsonCustomEnum("notification_failures_exceeded")]
        NotificationFailuresExceeded,
        /// <summary>
        /// The authorization was revoked for one or more users specified in the <see cref="Condition"/> object.
        /// </summary>
        [JsonCustomEnum("authorization_revoked")]
        AuthorizationRevoked,
        /// <summary>
        /// The moderator that authorized the subscription is no longer one of the broadcaster's moderators.
        /// </summary>
        [JsonCustomEnum("moderator_removed")]
        ModeratorRemoved,
        /// <summary>
        /// One of the users specified in the <see cref="Condition"/> object was removed.
        /// </summary>
        [JsonCustomEnum("user_removed")]
        UserRemoved,
        /// <summary>
        /// The subscription to subscription type and version is no longer supported.
        /// </summary>
        [JsonCustomEnum("version_removed")]
        VersionRemoved,
        /// <summary>
        /// The subscription to the beta subscription type was removed due to maintenance.
        /// </summary>
        [JsonCustomEnum("beta_maintenance")]
        BetaMaintenance,
        /// <summary>
        /// The client closed the connection.
        /// </summary>
        [JsonCustomEnum("websocket_disconnected")]
        WebsocketDisconnected,
        /// <summary>
        /// The client failed to respond to a ping message.
        /// </summary>
        [JsonCustomEnum("websocket_failed_ping_pong")]
        WebsocketFailedPingPong,
        /// <summary>
        /// The client sent a non-pong message. Clients may only send pong messages (and only in response to a ping message).
        /// </summary>
        [JsonCustomEnum("websocket_received_inbound_traffic")]
        WebsocketReceivedInboundTraffic,
        /// <summary>
        /// The client failed to subscribe to events within the required time.
        /// </summary>
        [JsonCustomEnum("websocket_connection_unused")]
        WebsocketConnectionUnused,
        /// <summary>
        /// The Twitch WebSocket server experienced an unexpected error.
        /// </summary>
        [JsonCustomEnum("websocket_internal_error")]
        WebsocketInternalError,
        /// <summary>
        /// The Twitch WebSocket server timed out writing the message to the client.
        /// </summary>
        [JsonCustomEnum("websocket_network_timeout")]
        WebsocketNetworkTimeout,
        /// <summary>
        /// The Twitch WebSocket server experienced a network error writing the message to the client.
        /// </summary>
        [JsonCustomEnum("websocket_network_error")]
        WebsocketNetworkError
    }

    public static async Task<ResponseData<EventSubSubscription>?> GetEventSubSubscriptions(TwitchSession session, SubscriptionStatus? status, string type, string userId, string after)
    {

    }
}
