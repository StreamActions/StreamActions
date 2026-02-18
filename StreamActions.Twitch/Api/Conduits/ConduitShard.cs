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
using StreamActions.Twitch.Api.Common;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Conduits;

/// <summary>
/// Represents a shard in a conduit.
/// </summary>
public sealed record ConduitShard
{
    /// <summary>
    /// Shard ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The shard's status.
    /// </summary>
    [JsonPropertyName("status")]
    public ShardStatus? Status { get; init; }

    /// <summary>
    /// The transport details used to send the notifications.
    /// </summary>
    [JsonPropertyName("transport")]
    public ConduitShardTransport? Transport { get; init; }

    /// <summary>
    /// The shard's status.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<ShardStatus>))]
    public enum ShardStatus
    {
        /// <summary>
        /// The shard is enabled.
        /// </summary>
        [JsonCustomEnum("enabled")]
        Enabled,
        /// <summary>
        /// The shard is pending verification of the specified callback URL.
        /// </summary>
        [JsonCustomEnum("webhook_callback_verification_pending")]
        WebhookCallbackVerificationPending,
        /// <summary>
        /// The shard failed verification of the specified callback URL.
        /// </summary>
        [JsonCustomEnum("webhook_callback_verification_failed")]
        WebhookCallbackVerificationFailed,
        /// <summary>
        /// The shard is disconnected from the WebSocket server.
        /// </summary>
        [JsonCustomEnum("websocket_disconnected")]
        WebsocketDisconnected,
        /// <summary>
        /// The shard failed to respond to a ping message.
        /// </summary>
        [JsonCustomEnum("websocket_failed_ping_pong")]
        WebsocketFailedPingPong,
        /// <summary>
        /// The shard received too much inbound traffic.
        /// </summary>
        [JsonCustomEnum("websocket_received_inbound_traffic_overflow")]
        WebsocketReceivedInboundTrafficOverflow,
        /// <summary>
        /// The shard's connection was unused.
        /// </summary>
        [JsonCustomEnum("websocket_connection_unused")]
        WebsocketConnectionUnused,
        /// <summary>
        /// The shard experienced an internal error.
        /// </summary>
        [JsonCustomEnum("websocket_internal_error")]
        WebsocketInternalError,
        /// <summary>
        /// The shard experienced a network timeout.
        /// </summary>
        [JsonCustomEnum("websocket_network_timeout")]
        WebsocketNetworkTimeout,
        /// <summary>
        /// The shard experienced a network error.
        /// </summary>
        [JsonCustomEnum("websocket_network_error")]
        WebsocketNetworkError
    }

    /// <summary>
    /// Gets a list of conduit shards.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="conduitId">Conduit ID.</param>
    /// <param name="status">Status to filter by.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ConduitShard"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="conduitId"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved conduit shards.</description>
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
    public static async Task<ResponseData<ConduitShard>?> GetConduitShards(TwitchSession session, Guid conduitId, ShardStatus? status = null, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (conduitId == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(conduitId)).Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        NameValueCollection queryParams = new()
        {
            { "conduit_id", conduitId.ToString("D", CultureInfo.InvariantCulture) }
        };

        if (status.HasValue)
        {
            queryParams.Add("status", status.Value.JsonValue());
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/eventsub/conduits/shards"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ConduitShard>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates conduit shards.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The parameters for the update.</param>
    /// <returns>A <see cref="ConduitShardUpdateResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <see cref="ConduitShardUpdateParameters.ConduitId"/> or <see cref="ConduitShardUpdateParameters.Shards"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">Any shard in <see cref="ConduitShardUpdateParameters.Shards"/> is missing an ID or transport.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ConduitShardUpdateParameters.Shards"/> has more than 20 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the conduit shards.</description>
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
    /// <description>The specified conduit was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ConduitShardUpdateResponse?> UpdateConduitShards(TwitchSession session, ConduitShardUpdateParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (!parameters.ConduitId.HasValue || parameters.ConduitId == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(parameters.ConduitId)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Shards is null)
        {
            throw new ArgumentNullException(nameof(parameters.Shards)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Shards.Count() > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Shards), parameters.Shards.Count(), "must have a count <= 20").Log(TwitchApi.GetLogger());
        }

        foreach (ConduitShardUpdateShardParameters shard in parameters.Shards)
        {
            if (string.IsNullOrWhiteSpace(shard.Id))
            {
                throw new ArgumentNullException(nameof(shard.Id)).Log(TwitchApi.GetLogger());
            }

            if (shard.Transport is null)
            {
                throw new ArgumentNullException(nameof(shard.Transport)).Log(TwitchApi.GetLogger());
            }
        }

        session.RequireAppToken();

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        Uri uri = Util.BuildUri(new("/eventsub/conduits/shards"));
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ConduitShardUpdateResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
