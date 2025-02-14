/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Subscriptions;

/// <summary>
/// Sends and represents a response element for a request for a list of subscribers.
/// </summary>
public sealed record Subscription
{
    /// <summary>
    /// An ID that identifies the broadcaster.
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
    /// The ID of the user that gifted the subscription to the user.
    /// </summary>
    [JsonPropertyName("gifter_id")]
    public string? GifterId { get; init; }

    /// <summary>
    /// The gifter's login name.
    /// </summary>
    [JsonPropertyName("gifter_login")]
    public string? GifterLogin { get; init; }

    /// <summary>
    /// The gifter's display name.
    /// </summary>
    [JsonPropertyName("gifter_name")]
    public string? GifterName { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the subscription is a gift subscription.
    /// </summary>
    [JsonPropertyName("is_gift")]
    public bool? IsGift { get; init; }

    /// <summary>
    /// The name of the subscription.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is not defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("plan_name")]
    public string? PlanName { get; init; }

    /// <summary>
    /// The type of subscription.
    /// </summary>
    [JsonPropertyName("tier")]
    public SubscriptionTier? Tier { get; init; }

    /// <summary>
    /// An ID that identifies the subscribing user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is not defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is not defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user's login name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is not defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The type of subscription.
    /// </summary>
    public enum SubscriptionTier
    {
        /// <summary>
        /// Default value. Not valid for use.
        /// </summary>
        None,
        /// <summary>
        /// Tier 1 or Prime subscription.
        /// </summary>
        Tier1 = 1000,
        /// <summary>
        /// Tier 2 subscription.
        /// </summary>
        Tier2 = 2000,
        /// <summary>
        /// Tier 3 subscription.
        /// </summary>
        Tier3 = 3000
    }

    /// <summary>
    /// Gets a list of users that subscribe to the specified broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The broadcaster's ID. This ID must match the user ID in the access token.</param>
    /// <param name="userId">Filters the list to include only the specified subscribers. You may specify a maximum of 100 subscribers.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Maximum: 100. Default: 20.</param>
    /// <param name="before">The cursor used to get the previous page of results.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="SubscriptionResponse"/> with elements of type <see cref="Subscription"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> is defined and has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException">More than one of <paramref name="userId"/>, <paramref name="after"/>, <paramref name="before"/> were defined.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadSubscriptions"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's list of subscribers.</description>
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
    public static async Task<SubscriptionResponse?> GetBroadcasterSubscriptions(TwitchSession session, string broadcasterId, IEnumerable<string>? userId = null, int first = 20, string? before = null, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (userId is not null && userId.Count() > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), userId.Count(), "must have a count <= 100").Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelReadSubscriptions);

        first = Math.Clamp(first, 1, 100);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (userId is not null && userId.Any())
        {
            List<string> userIds = [];
            foreach (string pollId in userId)
            {
                if (!string.IsNullOrWhiteSpace(pollId))
                {
                    userIds.Add(pollId);
                }
            }

            if (userIds.Count != 0)
            {
                if (!string.IsNullOrWhiteSpace(after))
                {
                    throw new InvalidOperationException(nameof(userId) + " can not be used at the same time as " + nameof(after)).Log(TwitchApi.GetLogger());
                }
                else if (!string.IsNullOrWhiteSpace(before))
                {
                    throw new InvalidOperationException(nameof(userId) + " can not be used at the same time as " + nameof(before)).Log(TwitchApi.GetLogger());
                }

                queryParams.Add("user_id", userIds);
            }
        }

        if (!string.IsNullOrWhiteSpace(before))
        {
            if (!string.IsNullOrWhiteSpace(after))
            {
                throw new InvalidOperationException(nameof(before) + " can not be used at the same time as " + nameof(after)).Log(TwitchApi.GetLogger());
            }

            queryParams.Add("before", before);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/subscriptions"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<SubscriptionResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks whether the user subscribes to the broadcaster's channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of a partner or affiliate broadcaster.</param>
    /// <param name="userId">The ID of the user that you're checking to see whether they subscribe to the broadcaster in <paramref name="broadcasterId"/>. This ID must match the user ID in the access Token.</param>
    /// <returns>A <see cref="SubscriptionResponse"/> with elements of type <see cref="Subscription"/> containing the response. The response contains a <see cref="JsonApiResponse.Status"/> of <see cref="System.Net.HttpStatusCode.NotFound"/> if the <paramref name="userId"/> is not subscribed to <paramref name="broadcasterId"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserReadSubscriptions"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>The user subscribes to the broadcaster.</description>
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
    /// <description>The user does not subscribe to the broadcaster.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<SubscriptionResponse?> CheckUserSubscription(TwitchSession session, string broadcasterId, string userId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.UserReadSubscriptions);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "user_id", userId }
        };

        Uri uri = Util.BuildUri(new("/subscriptions/user"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<SubscriptionResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
