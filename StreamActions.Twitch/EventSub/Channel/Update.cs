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

using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel;

/// <summary>
/// An event that is sent when a broadcaster updates their channel information.
/// </summary>
public sealed record Update : IEventSubType
{
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    public static string Type => "channel.update";

    public static string Version => "2";

    /// <summary>
    /// The ID of the broadcaster that updated their channel information.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The login name of the broadcaster that updated their channel information.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The display name of the broadcaster that updated their channel information.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The channel's stream title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The channel's broadcast language.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// The ID of the category that the channel is currently streaming in.
    /// </summary>
    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }

    /// <summary>
    /// The name of the category that the channel is currently streaming in.
    /// </summary>
    [JsonPropertyName("category_name")]
    public string? CategoryName { get; init; }

    /// <summary>
    /// The channel's content classification label IDs.
    /// </summary>
    [JsonPropertyName("content_classification_labels")]
    public IEnumerable<string>? ContentClassificationLabels { get; init; }
}
