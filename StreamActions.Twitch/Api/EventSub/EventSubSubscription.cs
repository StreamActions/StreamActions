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

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.EventSub;

/// <summary>
/// Sends and represents a response element for a request for EventSub Subscriptions.
/// </summary>
[JsonConverter(typeof(EventSubSubscriptionConverter))]
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
    /// The subscription's parameter values.
    /// </summary>
    [JsonPropertyName("condition")]
    public EventSubCondition? Condition { get; init; }

    /// <summary>
    /// The date and time of when the subscription was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The transport details used to send the notifications.
    /// </summary>
    [JsonPropertyName("transport")]
    public EventSubTransport? Transport { get; init; }

    /// <summary>
    /// The amount that the subscription counts against the limit.
    /// </summary>
    [JsonPropertyName("cost")]
    public int? Cost { get; init; }

    /// <summary>
    /// EventSub Subscription Status.
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

    /// <summary>
    /// Gets a list of EventSub subscriptions that the client in the access token created.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="status">Filter subscriptions by its status.</param>
    /// <param name="type">Filter subscriptions by subscription type.</param>
    /// <param name="userId">Filter subscriptions by a user ID in the condition.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="EventSubSubscription"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">More than one of <paramref name="status"/>, <paramref name="type"/>, <paramref name="userId"/> were defined.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// If the <see cref="EventSubTransport.Method"/> is set to <see cref="EventSubTransport.TransportMethod.Webhook"/> or <see cref="EventSubTransport.TransportMethod.Conduit"/>, the <see cref="TwitchSession.Token"/> must be an <em>app access</em> token, otherwise the request will fail with <strong>401 Unauthorized</strong> or exclude the subscription from the results.
    /// </para>
    /// <para>
    /// If the <see cref="EventSubTransport.Method"/> is set to <see cref="EventSubTransport.TransportMethod.Websocket"/>, the <see cref="TwitchSession.Token"/> must be a <em>user access</em> token, otherwise the request will fail with <strong>401 Unauthorized</strong> or exclude the subscription from the results. The scopes on a <em>user access</em> token used for this API call do not have to match the required scopes of the retrieved subscriptions.
    /// </para>
    /// <para>
    /// If multiple <see cref="EventSubTransport.Method"/> are used across the application instance or <see cref="EventSubTransport.TransportMethod.Websocket"/> is used with <em>user access</em> tokens, multiple calls with each different access token will be required to retrieve all subscriptions.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the subscriptions.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<EventSubSubscription>?> GetEventSubSubscriptions(TwitchSession session, SubscriptionStatus? status = null, string? type = null, string? userId = null, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        NameValueCollection queryParams = [];

        if (status.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(type))
            {
                throw new InvalidOperationException(nameof(status) + " can not be used at the same time as " + nameof(type)).Log(TwitchApi.GetLogger());
            }
            else if (!string.IsNullOrWhiteSpace(userId))
            {
                throw new InvalidOperationException(nameof(status) + " can not be used at the same time as " + nameof(userId)).Log(TwitchApi.GetLogger());
            }

            queryParams.Add("status", status.Value.JsonValue());
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                throw new InvalidOperationException(nameof(type) + " can not be used at the same time as " + nameof(userId)).Log(TwitchApi.GetLogger());
            }

            queryParams.Add("type", type);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            queryParams.Add("user_id", userId);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/eventsub/subscriptions"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<EventSubSubscription>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an EventSub subscription.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The subscription to create.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="EventSubSubscription"/> containing the response.</returns>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code of the request.</returns>
    /// <exception cref="ArgumentNullException">Any parameter is <see langword="null"/>; <see cref="EventSubSubscriptionCreationParameters.Transport"/> is missing a required parameter for the selected <see cref="EventSubTransport.TransportMethod"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="EventSubTransport.Method"/> is set to <see cref="EventSubTransport.TransportMethod.Webhook"/> and the length of <see cref="EventSubTransport.Secret"/> is &lt; 10 or > 100.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// If <see cref="EventSubTransport.Method"/> is set to <see cref="EventSubTransport.TransportMethod.Webhook"/> or <see cref="EventSubTransport.TransportMethod.Conduit"/>, the <see cref="TwitchSession.Token"/> must be an <em>app access</em> token, otherwise the request will fail.
    /// Additionally, the app must have already separately authorized a <em>user access</em> token which includes the required scopes for the chosen <see cref="EventSubSubscriptionCreationParameters.Type"/>.
    /// </para>
    /// <para>
    /// If <see cref="EventSubTransport.Method"/> is set to <see cref="EventSubTransport.TransportMethod.Websocket"/>, the <see cref="TwitchSession.Token"/> must be a <em>user access</em> token, otherwise the request will fail.
    /// Additionally, the token must include the required scopes for the chosen <see cref="EventSubSubscriptionCreationParameters.Type"/>.
    /// </para>
    /// <para>
    /// NOTE: This member does not perform checks on the <see cref="EventSubCondition"/> for missing parameters, check the documentation and response data for errors.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>202 Accepted</term>
    /// <description>Successfully accepted the subscription request.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>The OAuth token was invalid for this request due missing scopes.</description>
    /// </item>
    /// <item>
    /// <term>409 Conflict</term>
    /// <description>An active subscription with the same <see cref="EventSubSubscriptionCreationParameters.Type"/> and <see cref="EventSubSubscriptionCreationParameters.Condition"/> already exists.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description>Maximum number of active subscriptions with the same <see cref="EventSubSubscriptionCreationParameters.Type"/> and <see cref="EventSubSubscriptionCreationParameters.Condition"/> already reached.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<EventSubSubscription>?> CreateEventSubSubscription(TwitchSession session, EventSubSubscriptionCreationParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        if (string.IsNullOrEmpty(parameters.Type))
        {
            throw new ArgumentNullException(nameof(parameters.Type)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrEmpty(parameters.Version))
        {
            throw new ArgumentNullException(nameof(parameters.Version)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Condition is null)
        {
            throw new ArgumentNullException(nameof(parameters.Condition)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Transport is null)
        {
            throw new ArgumentNullException(nameof(parameters.Transport)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Transport.Method is null)
        {
            throw new ArgumentNullException(nameof(parameters.Transport.Method)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Transport.Method is EventSubTransport.TransportMethod.Conduit && (!parameters.Transport.ConduitId.HasValue || parameters.Transport.ConduitId == Guid.Empty))
        {
            throw new ArgumentNullException(nameof(parameters.Transport.ConduitId)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Transport.Method is EventSubTransport.TransportMethod.Webhook)
        {
            if (string.IsNullOrWhiteSpace(parameters.Transport.Callback))
            {
                throw new ArgumentNullException(nameof(parameters.Transport.Callback)).Log(TwitchApi.GetLogger());
            }

            if (string.IsNullOrWhiteSpace(parameters.Transport.Secret))
            {
                throw new ArgumentNullException(nameof(parameters.Transport.Secret)).Log(TwitchApi.GetLogger());
            }

            if (parameters.Transport.Secret.Length is < 10 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(parameters.Transport.Secret), parameters.Transport.Secret.Length, "length must be >= 10 and <= 100").Log(TwitchApi.GetLogger());
            }
        }

        if (parameters.Transport.Method is EventSubTransport.TransportMethod.Websocket && string.IsNullOrWhiteSpace(parameters.Transport.SessionId))
        {
            throw new ArgumentNullException(nameof(parameters.Transport.SessionId)).Log(TwitchApi.GetLogger());
        }

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        Uri uri = Util.BuildUri(new("/eventsub/subscriptions"));
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<EventSubSubscription>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes an EventSub subscription.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="id">The ID of the subscription to delete.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code of the request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// If the <see cref="EventSubTransport.Method"/> is set to <see cref="EventSubTransport.TransportMethod.Webhook"/> or <see cref="EventSubTransport.TransportMethod.Conduit"/>, the <see cref="TwitchSession.Token"/> must be an <em>app access</em> token, otherwise the request will fail with <strong>401 Unauthorized</strong>.
    /// </para>
    /// <para>
    /// If the <see cref="EventSubTransport.Method"/> is set to <see cref="EventSubTransport.TransportMethod.Websocket"/>, the <see cref="TwitchSession.Token"/> must be a <em>user access</em> token from the same user as the subscription, otherwise the request will fail with <strong>401 Unauthorized</strong>. The scopes on a <em>user access</em> token used for this API call do not have to match the required scopes of the deleted subscription.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully deleted the subscription.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The subscription was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> DeleteEventSubSubscription(TwitchSession session, Guid id)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id)).Log(TwitchApi.GetLogger());
        }

        Uri uri = Util.BuildUri(new("/eventsub/subscriptions"), new() { { "id", id.ToString() } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
