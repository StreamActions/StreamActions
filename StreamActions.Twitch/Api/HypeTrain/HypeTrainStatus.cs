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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.HypeTrain;

/// <summary>
/// Sends and represents a response element from a request for hype train status.
/// </summary>
public sealed record HypeTrainStatus
{
    /// <summary>
    /// An object describing the current Hype Train. Null if a Hype Train is not active.
    /// </summary>
    [JsonPropertyName("current")]
    public HypeTrain? Current { get; init; }

    /// <summary>
    /// An object with information about the channel's Hype Train records. Null if a Hype Train has not occurred.
    /// </summary>
    [JsonPropertyName("all_time_high")]
    public HypeTrainRecord? AllTimeHigh { get; init; }

    /// <summary>
    /// An object with information about the channel's shared Hype Train records. Null if a Hype Train has not occurred.
    /// </summary>
    [JsonPropertyName("shared_all_time_high")]
    public HypeTrainRecord? SharedAllTimeHigh { get; init; }

    /// <summary>
    /// Get the status of a Hype Train for the specified broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The User ID of the channel broadcaster.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="HypeTrainStatus"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadHypeTrain"/>.</exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the status object.</description>
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
    public static async Task<ResponseData<HypeTrainStatus>?> GetHypeTrainStatus(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelReadHypeTrain);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId }
        };

        Uri uri = Util.BuildUri(new("/hypetrain/status"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<HypeTrainStatus>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
