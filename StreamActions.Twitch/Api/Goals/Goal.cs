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
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Goals;

/// <summary>
/// Sends and represents a response element for a request for creator goals.
/// </summary>
public sealed record Goal
{
    /// <summary>
    /// An ID that identifies this goal.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that identifies the broadcaster that created the goal.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The type of goal.
    /// </summary>
    [JsonPropertyName("type")]
    public GoalType? Type { get; init; }

    /// <summary>
    /// A description of the goal. Is an empty string if not specified.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// The goal's current value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If type is <see cref="GoalType.Follower"/>, this field is set to the broadcaster's current number of followers. This number increases with new followers and decreases when users unfollow the broadcaster.
    /// </para>
    /// <para>
    /// If type is <see cref="GoalType.Subscription"/>, this field is increased and decreased by the points value associated with the subscription tier. For example, if a tier-two subscription is worth 2 points, this field is increased or decreased by 2, not 1.
    /// </para>
    /// <para>
    /// If type is <see cref="GoalType.SubscriptionCount"/>, this field is increased by 1 for each new subscription and decreased by 1 for each user that unsubscribes.
    /// </para>
    /// <para>
    /// If type is <see cref="GoalType.NewSubscription"/>, this field is increased by the points value associated with the subscription tier. For example, if a tier-two subscription is worth 2 points, this field is increased by 2, not 1.
    /// </para>
    /// <para>
    /// If type is <see cref="GoalType.NewSubscriptionCount"/>, this field is increased by 1 for each new subscription.
    /// </para>
    /// </remarks>
    [JsonPropertyName("current_amount")]
    public int? CurrentAmount { get; init; }

    /// <summary>
    /// The goal's target value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For example, if the broadcaster has 200 followers before creating the goal, and their goal is to double that number, this field is set to 400.
    /// </para>
    /// </remarks>
    [JsonPropertyName("target_amount")]
    public int? TargetAmount { get; init; }

    /// <summary>
    /// The UTC date and time that the broadcaster created the goal.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The type of goal.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<GoalType>))]
    public enum GoalType
    {
        /// <summary>
        /// The goal is to increase followers.
        /// </summary>
        [JsonCustomEnum("follower")]
        Follower,
        /// <summary>
        /// The goal is to increase subscriptions. This type shows the net increase or decrease in tier points associated with the subscriptions.
        /// </summary>
        [JsonCustomEnum("subscription")]
        Subscription,
        /// <summary>
        /// The goal is to increase subscriptions. This type shows the net increase or decrease in the number of subscriptions.
        /// </summary>
        [JsonCustomEnum("subscription_count")]
        SubscriptionCount,
        /// <summary>
        /// The goal is to increase subscriptions. This type shows only the net increase in tier points associated with the subscriptions (it does not account for users that unsubscribed since the goal started).
        /// </summary>
        [JsonCustomEnum("new_subscription")]
        NewSubscription,
        /// <summary>
        /// The goal is to increase subscriptions. This type shows only the net increase in the number of subscriptions (it does not account for users that unsubscribed since the goal started).
        /// </summary>
        [JsonCustomEnum("new_subscription_count")]
        NewSubscriptionCount
    }

    /// <summary>
    /// Gets the broadcaster's list of active goals. Use this endpoint to get the current progress of each goal.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that created the goals. This ID must match the user ID in the user access token.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Goal"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadGoals"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's goals.</description>
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
    public static async Task<ResponseData<Goal>?> GetCreatorGoals(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelReadGoals);

        Uri uri = Util.BuildUri(new("/goals"), new() { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Goal>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
