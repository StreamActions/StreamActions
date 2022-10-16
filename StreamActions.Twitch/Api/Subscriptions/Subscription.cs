/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Exceptions;
using StreamActions.Twitch.Api.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Subscriptions
{
    /// <summary>
    /// Sends and represents a response element for a list of subscribers.
    /// </summary>
    public record Subscription
    {
        /// <summary>
        /// User ID of the broadcaster.
        /// </summary>
        [JsonPropertyName("broadcaster_id")]
        public string? BroadcasterId { get; init; }

        /// <summary>
        /// Login of the broadcaster.
        /// </summary>
        [JsonPropertyName("broadcaster_login")]
        public string? BroadcasterLogin { get; init; }

        /// <summary>
        /// Display name of the broadcaster.
        /// </summary>
        [JsonPropertyName("broadcaster_name")]
        public string? BroadcasterName { get; init; }

        /// <summary>
        /// If the subscription was gifted, this is the user ID of the gifter. May not be defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
        /// </summary>
        [JsonPropertyName("gifter_id")]
        public string? GifterId { get; init; }

        /// <summary>
        /// If the subscription was gifted, this is the login of the gifter.
        /// </summary>
        [JsonPropertyName("gifter_login")]
        public string? GifterLogin { get; init; }

        /// <summary>
        /// If the subscription was gifted, this is the display name of the gifter.
        /// </summary>
        [JsonPropertyName("gifter_name")]
        public string? GifterName { get; init; }

        /// <summary>
        /// Is <see langword="true"/> if the subscription is a gift subscription.
        /// </summary>
        [JsonPropertyName("is_gift")]
        public bool? IsGift { get; init; }

        /// <summary>
        /// Name of the subscription. May not be defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
        /// </summary>
        [JsonPropertyName("plan_name")]
        public string? PlanName { get; init; }

        /// <summary>
        /// Type of subscription (Tier 1, Tier 2, Tier 3).
        /// </summary>
        [JsonPropertyName("tier")]
        public SubscriptionTier? Tier { get; init; }

        /// <summary>
        /// ID of the subscribed user. May not be defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string? UserId { get; init; }

        /// <summary>
        /// Display name of the subscribed user. May not be defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
        /// </summary>
        [JsonPropertyName("user_login")]
        public string? UserLogin { get; init; }

        /// <summary>
        /// Login of the subscribed user. May not be defined for <see cref="CheckUserSubscription(TwitchSession, string, string)"/>.
        /// </summary>
        [JsonPropertyName("user_name")]
        public string? UserName { get; init; }

        /// <summary>
        /// Subscription tiers.
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
        /// <param name="broadcasterId">User ID of the broadcaster. Must match the User ID in the Bearer token.</param>
        /// <param name="userId">Filters the list to include only the specified subscribers. You may specify a maximum of 100 subscribers.</param>
        /// <param name="after">Cursor for forward pagination: tells the server where to start fetching the next set of results in a multi-page response. This may only be used on queries without <paramref name="userId"/>. The cursor value specified here is from the pagination response field of a prior query.</param>
        /// <param name="first">Maximum number of objects to return. Maximum: 100. Default: 20.</param>
        /// <returns>A <see cref="SubscriptionResponse"/> with elements of type <see cref="Subscription"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> is defined and has more than 100 elements.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="userId"/> and <paramref name="after"/> are both defined.</exception>
        /// <exception cref="ScopeMissingException"><paramref name="session"/> contains a scope list, and <c>channel:read:subscriptions</c> is not present.</exception>
        public static async Task<SubscriptionResponse?> GetBroadcasterSubscriptions(TwitchSession session, string broadcasterId, IEnumerable<string>? userId = null, string? after = null, int first = 20)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            if (userId is not null && userId.Count() > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(userId), "must have a count <= 100");
            }

            if (!session.Token?.HasScope("channel:read:subscriptions") ?? false)
            {
                throw new ScopeMissingException("channel:read:subscriptions");
            }

            first = Math.Clamp(first, 1, 100);

            Dictionary<string, IEnumerable<string>> queryParams = new()
            {
                { "broadcaster_id", new List<string> { broadcasterId } },
                { "first", new List<string> { first.ToString(CultureInfo.InvariantCulture) } }
            };

            if (userId is not null && userId.Any())
            {
                List<string> userIds = new();
                foreach (string pollId in userId)
                {
                    if (!string.IsNullOrWhiteSpace(pollId))
                    {
                        userIds.Add(pollId);
                    }
                }

                if (userIds.Any())
                {
                    if (!string.IsNullOrWhiteSpace(after))
                    {
                        throw new InvalidOperationException(nameof(userId) + " can not be used at the same time as " + nameof(after));
                    }

                    queryParams.Add("user_id", userIds);
                }
            }

            if (!string.IsNullOrWhiteSpace(after))
            {
                queryParams.Add("after", new List<string> { after });
            }

            Uri uri = Util.BuildUri(new("/subscriptions"), queryParams);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<SubscriptionResponse?>().ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if a specific user (<paramref name="userId"/>) is subscribed to a specific channel (<paramref name="broadcasterId"/>).
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="broadcasterId">User ID of the broadcaster. Must match the User ID in the Bearer token.</param>
        /// <param name="userId">User ID of a Twitch viewer.</param>
        /// <returns>A <see cref="SubscriptionResponse"/> with elements of type <see cref="Subscription"/> containing the response. The response contains a <see cref="TwitchResponse.Status"/> of <see cref="System.Net.HttpStatusCode.NotFound"/> if the <paramref name="userId"/> is not subscribed to <paramref name="broadcasterId"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> or <paramref name="userId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ScopeMissingException"><paramref name="session"/> contains a scope list, and <c>user:read:subscriptions</c> is not present.</exception>
        public static async Task<SubscriptionResponse?> CheckUserSubscription(TwitchSession session, string broadcasterId, string userId)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentNullException(nameof(userId));
            }

            if (!session.Token?.HasScope("user:read:subscriptions") ?? false)
            {
                throw new ScopeMissingException("user:read:subscriptions");
            }

            Dictionary<string, IEnumerable<string>> queryParams = new()
            {
                { "broadcaster_id", new List<string> { broadcasterId } },
                { "user_id", new List<string> { userId } }
            };

            Uri uri = Util.BuildUri(new("/subscriptions/user"), queryParams);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<SubscriptionResponse?>().ConfigureAwait(false);
        }
    }
}
