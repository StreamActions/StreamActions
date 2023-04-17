/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Drawing;
using System.Net.Http.Json;
using StreamActions.Common.Net;
using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;

namespace StreamActions.Twitch.Api.ChannelPoints;

/// <summary>
/// Sends and represents a response element for a request to update or get channel points reward redemptions.
/// </summary>
public sealed record ChannelPointsRedemption
{
    /// <summary>
    /// The ID that uniquely identifies the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The ID that uniquely identifies this redemption.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

    /// <summary>
    /// The ID that uniquely identifies the user that redeemed the reward.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The text the user entered at the prompt when they redeemed the reward; otherwise, an empty string if user input was not required.
    /// </summary>
    [JsonPropertyName("user_input")]
    public string? UserInput { get; init; }

    /// <summary>
    /// The state of the redemption.
    /// </summary>
    [JsonPropertyName("status")]
    public RedemptionStatus? Status { get; init; }

    /// <summary>
    /// The date and time of when the reward was redeemed.
    /// </summary>
    [JsonPropertyName("redeemed_at")]
    public DateTime? RedeemedAt { get; init; }

    /// <summary>
    /// The reward that the user redeemed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only these fields are available in the object:
    /// <list type="table">
    /// <item>
    /// <description><see cref="ChannelPointsReward.Id"/></description>
    /// </item>
    /// <item>
    /// <description><see cref="ChannelPointsReward.Title"/></description>
    /// </item>
    /// <item>
    /// <description><see cref="ChannelPointsReward.Prompt"/></description>
    /// </item>
    /// <item>
    /// <description><see cref="ChannelPointsReward.Cost"/></description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [JsonPropertyName("reward")]
    public ChannelPointsReward? Reward { get; init; }

    /// <summary>
    /// The state of the redemption.
    /// </summary>
    [JsonConverter(typeof(JsonUpperCaseEnumConverter<RedemptionStatus>))]
    public enum RedemptionStatus
    {
        /// <summary>
        /// Unfulfilled and in the request queue.
        /// </summary>
        Unfulfilled,
        /// <summary>
        /// Fulfilled.
        /// </summary>
        Fulfilled,
        /// <summary>
        /// Canceled and refunded.
        /// </summary>
        Canceled
    }

    /// <summary>
    /// The order to sort redemptions by.
    /// </summary>
    [JsonConverter(typeof(JsonUpperCaseEnumConverter<RedemptionSort>))]
    public enum RedemptionSort
    {
        /// <summary>
        /// Oldest redemptions first.
        /// </summary>
        Oldest,
        /// <summary>
        /// Newest redemptions first.
        /// </summary>
        Newest
    }

    /// <summary>
    /// Gets a list of redemptions for the specified custom reward.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the custom reward. This ID must match the user ID found in the user OAuth token.</param>
    /// <param name="rewardId">The ID that identifies the custom reward whose redemptions you want to get.</param>
    /// <param name="status">The status of the redemptions to return.</param>
    /// <param name="id">A list of IDs to filter the redemptions by. You may specify a maximum of 50 IDs.</param>
    /// <param name="sort">The order to sort redemptions by.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <param name="first">The maximum number of redemptions to return per page in the response. Maximum: 50. Default: 20.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelPointsReward"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="rewardId"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is defined and has more than 50 elements; <paramref name="status"/> or <paramref name="sort"/> are not valid values of their respective enums.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadRedemptions"/> or <see cref="Scope.ChannelManageRedemptions"/>.</exception>
    /// <remarks>
    /// <para>
    /// The app used to create the reward is the only app that may get the redemptions.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of redeemed custom rewards.</description>
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
    /// <description>The specified broadcaster is not a partner or affiliate; the client id of the OAuth token must match the one which created the reward.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The specified redemption ids do not exist.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ChannelPointsReward>?> GetCustomRewardRedemption(TwitchSession session, string broadcasterId, Guid rewardId, RedemptionStatus status = RedemptionStatus.Unfulfilled, IEnumerable<Guid>? id = null, RedemptionSort sort = RedemptionSort.Oldest, string? after = null, int first = 20)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (rewardId == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(rewardId)).Log(TwitchApi.GetLogger());
        }

        if (id is not null && id.Count() > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 50").Log(TwitchApi.GetLogger());
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "not a valid value of " + nameof(RedemptionStatus)).Log(TwitchApi.GetLogger());
        }

        if (!Enum.IsDefined(sort))
        {
            throw new ArgumentOutOfRangeException(nameof(sort), sort, "not a valid value of " + nameof(RedemptionSort)).Log(TwitchApi.GetLogger());
        }

        first = Math.Clamp(first, 1, 50);

        session.RequireToken(Scope.ChannelReadRedemptions, Scope.ChannelManageRedemptions);

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "broadcaster_id", new List<string> { broadcasterId } },
            { "reward_id", new List<string> { rewardId.ToString("D", CultureInfo.InvariantCulture) } },
            { "sort", new List<string> { sort.JsonValue() ?? "" } }
        };

        if (id is not null && id.Any())
        {
            queryParams.Add("id", id.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
        }
        else
        {
            queryParams.Add("status", new List<string> { status.JsonValue() ?? "" });
            queryParams.Add("first", new List<string> { first.ToString(CultureInfo.InvariantCulture) });
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", new List<string> { after });
        }

        Uri uri = Util.BuildUri(new("/channel_points/custom_rewards/redemptions"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChannelPointsReward>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a list of redemptions for the specified custom reward.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="id">A list of IDs that identify the redemptions to update. You may specify a maximum of 50 IDs.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that's updating the redemption. This ID must match the user ID in the user access token.</param>
    /// <param name="rewardId">The ID that identifies the reward that's been redeemed.</param>
    /// <param name="parameters">The parameters describing the new state of the redemptions.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelPointsReward"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="rewardId"/> is <see cref="Guid.Empty"/>; <paramref name="id"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> has more than 50 elements; <see cref="ChannelPointsRedemptionUpdateParameters.Status"/> is not <see cref="RedemptionStatus.Fulfilled"/> or <see cref="RedemptionStatus.Canceled"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadRedemptions"/> or <see cref="Scope.ChannelManageRedemptions"/>.</exception>
    /// <remarks>
    /// <para>
    /// The app used to create the reward is the only app that may update the redemption.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the redemption's status.</description>
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
    /// <description>The specified broadcaster is not a partner or affiliate; the client id of the OAuth token must match the one which created the reward.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The specified reward does not exist; the specified redemption ids do not exist or are not <see cref="RedemptionStatus.Unfulfilled"/>.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ChannelPointsReward>?> UpdateRedemptionStatus(TwitchSession session, IEnumerable<Guid> id, string broadcasterId, Guid rewardId, ChannelPointsRedemptionUpdateParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (rewardId == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(rewardId)).Log(TwitchApi.GetLogger());
        }

        if (id is null)
        {
            throw new ArgumentNullException(nameof(id)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (id.Count() > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 50").Log(TwitchApi.GetLogger());
        }

        if (parameters.Status is not RedemptionStatus.Fulfilled or RedemptionStatus.Canceled)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Status), parameters.Status, "must be Fulfilled or Canceled").Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManageRedemptions);

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "broadcaster_id", new List<string> { broadcasterId } },
            { "reward_id", new List<string> { rewardId.ToString("D", CultureInfo.InvariantCulture) } },
            { "id", id.Select(x => x.ToString("D", CultureInfo.InvariantCulture)) }
        };

        Uri uri = Util.BuildUri(new("/channel_points/custom_rewards/redemptions"), queryParams);
        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChannelPointsReward>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
