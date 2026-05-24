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

using StreamActions.Common.Json.Serialization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.HypeTrain;

/// <summary>
/// Represents a contribution to a Hype Train.
/// </summary>
public sealed record HypeTrainContribution
{
    /// <summary>
    /// The ID of the user that made the contribution.
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
    /// The contribution method used.
    /// </summary>
    [JsonPropertyName("type")]
    public HypeTrainContributionType? Type { get; init; }

    /// <summary>
    /// The total number of points contributed for the type.
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; init; }

    /// <summary>
    /// The contribution method used.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<HypeTrainContributionType>))]
    public enum HypeTrainContributionType
    {
        /// <summary>
        /// Cheering with Bits.
        /// </summary>
        [JsonCustomEnum("BITS")]
        Bits,

        /// <summary>
        /// Subscription activity like subscribing or gifting subscriptions.
        /// </summary>
        [JsonCustomEnum("SUBS")]
        Subscription,

        /// <summary>
        /// Covers other contribution methods not listed.
        /// </summary>
        [JsonCustomEnum("OTHER")]
        Other
    }
}
