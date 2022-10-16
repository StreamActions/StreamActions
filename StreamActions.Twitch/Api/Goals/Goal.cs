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
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Goals
{
    /// <summary>
    /// Sends and represents a response element for a request for creator goals.
    /// </summary>
    public record Goal
    {
        /// <summary>
        /// An ID that uniquely identifies this goal.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        /// <summary>
        /// An ID that uniquely identifies the broadcaster.
        /// </summary>
        [JsonPropertyName("broadcaster_id")]
        public string? BroadcasterId { get; init; }

        /// <summary>
        /// The broadcaster's display name.
        /// </summary>
        [JsonPropertyName("broadcaster_name")]
        public string? BroadcasterName { get; init; }

        /// <summary>
        /// The broadcaster's user handle.
        /// </summary>
        [JsonPropertyName("broadcaster_login")]
        public string? BroadcasterLogin { get; init; }

        /// <summary>
        /// The type of goal.
        /// </summary>
        [JsonPropertyName("type")]
        public GoalType? Type { get; init; }

        /// <summary>
        /// A description of the goal, if specified. The description may contain a maximum of 40 characters.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; init; }

        /// <summary>
        /// The current value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If type is <see cref="GoalType.Follower"/>, this field is set to the broadcaster's current number of followers. This number increases with new followers and decreases when users unfollow the broadcaster.
        /// </para>
        /// <para>
        /// If type is <see cref="GoalType.Subscription"/>, this field is increased and decreased by the points value associated with the subscription tier. For example, if a tier-two subscription is worth 2 points, this field is increased or decreased by 2, not 1.
        /// </para>
        /// <para>
        /// If type is <see cref="GoalType.Subscription_Count"/>, this field is increased by 1 for each new subscription and decreased by 1 for each user that unsubscribes.
        /// </para>
        /// <para>
        /// If type is <see cref="GoalType.New_Subscription"/>, this field is increased by the points value associated with the subscription tier. For example, if a tier-two subscription is worth 2 points, this field is increased by 2, not 1.
        /// </para>
        /// <para>
        /// If type is <see cref="GoalType.New_Subscription_Count"/>, this field is increased by 1 for each new subscription.
        /// </para>
        /// </remarks>
        [JsonPropertyName("current_amount")]
        public int? CurrentAmount { get; init; }

        /// <summary>
        /// The goal's target value. For example, if the broadcaster has 200 followers before creating the goal, and their goal is to double that number, this field is set to 400.
        /// </summary>
        [JsonPropertyName("target_amount")]
        public int? TargetAmount { get; init; }

        /// <summary>
        /// The UTC timestamp in RFC 3339 format, which indicates when the broadcaster created the goal.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; init; }

        /// <summary>
        /// The type of goal.
        /// </summary>
        public enum GoalType
        {
            /// <summary>
            /// The goal is to increase followers.
            /// </summary>
            Follower,
            /// <summary>
            /// The goal is to increase subscriptions. This type shows the net increase or decrease in tier points associated with the subscriptions.
            /// </summary>
            Subscription,
            /// <summary>
            /// The goal is to increase subscriptions. This type shows the net increase or decrease in the number of subscriptions.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
            Subscription_Count,
            /// <summary>
            /// The goal is to increase subscriptions. This type shows only the net increase in tier points associated with the subscriptions (it does not account for users that unsubscribed since the goal started).
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
            New_Subscription,
            /// <summary>
            /// The goal is to increase subscriptions. This type shows only the net increase in the number of subscriptions (it does not account for users that unsubscribed since the goal started).
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
            New_Subscription_Count
        }

        /// <summary>
        /// Gets the broadcaster's list of active goals. Use this to get the current progress of each goal.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="broadcasterId">The ID of the broadcaster that created the goals.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Channel"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ScopeMissingException"><paramref name="session"/> contains a scope list, and <c>channel:read:goals</c> is not present.</exception>
        public static async Task<ResponseData<Goal>?> GetCreatorGoals(TwitchSession session, string broadcasterId)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            if (!session.Token?.HasScope("channel:read:goals") ?? false)
            {
                throw new ScopeMissingException("channel:read:goals");
            }

            Uri uri = Util.BuildUri(new("/goals"), new Dictionary<string, IEnumerable<string>> { { "broadcaster_id", new List<string> { broadcasterId } } });
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<Goal>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }
    }
}
