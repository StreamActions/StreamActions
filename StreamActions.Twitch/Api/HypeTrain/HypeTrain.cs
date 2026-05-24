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
/// Represents a Hype Train.
/// </summary>
public sealed record HypeTrain
{
    /// <summary>
    /// The Hype Train ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The broadcaster ID.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster login.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The broadcaster display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The current level of the Hype Train.
    /// </summary>
    [JsonPropertyName("level")]
    public int? Level { get; init; }

    /// <summary>
    /// Total points contributed to the Hype Train.
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; init; }

    /// <summary>
    /// The number of points contributed to the Hype Train at the current level.
    /// </summary>
    [JsonPropertyName("progress")]
    public int? Progress { get; init; }

    /// <summary>
    /// The number of points required to reach the next level.
    /// </summary>
    [JsonPropertyName("goal")]
    public int? Goal { get; init; }

    /// <summary>
    /// The contributors with the most points contributed.
    /// </summary>
    [JsonPropertyName("top_contributions")]
    public IReadOnlyList<HypeTrainContribution>? TopContributions { get; init; }

    /// <summary>
    /// A list containing the broadcasters participating in the shared Hype Train. Null if the Hype Train is not shared.
    /// </summary>
    [JsonPropertyName("shared_train_participants")]
    public IReadOnlyList<HypeTrainSharedParticipant>? SharedTrainParticipants { get; init; }

    /// <summary>
    /// The time when the Hype Train started.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// The time when the Hype Train expires. The expiration is extended when the Hype Train reaches a new level.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// The type of the Hype Train.
    /// </summary>
    [JsonPropertyName("type")]
    public HypeTrainType? Type { get; init; }

    /// <summary>
    /// Indicates if the Hype Train is shared. When true, <see cref="SharedTrainParticipants"/> will contain the list of broadcasters the train is shared with.
    /// </summary>
    [JsonPropertyName("is_shared_train")]
    public bool? IsSharedTrain { get; init; }

    /// <summary>
    /// The type of the Hype Train.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<HypeTrainType>))]
    public enum HypeTrainType
    {
        /// <summary>
        /// Treasure Hype Train.
        /// </summary>
        [JsonCustomEnum("treasure")]
        Treasure,

        /// <summary>
        /// Golden Kappa Hype Train.
        /// </summary>
        [JsonCustomEnum("golden_kappa")]
        GoldenKappa,

        /// <summary>
        /// Regular Hype Train.
        /// </summary>
        [JsonCustomEnum("regular")]
        Regular
    }
}
