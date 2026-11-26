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

using StreamActions.Twitch.Api.Channels.Objects;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel;

/// <summary>
/// An event that is sent when the category, title, content classification labels, or broadcast language of the broadcaster's channel changes.
/// </summary>
/// <remarks>
/// <para>
/// Changes made by the broadcaster, by one of their editors, or by Twitch (for example, when Twitch applies the MatureGame label because the channel's category is a mature-rated game) all send notifications.
/// </para>
/// <para>
/// A single update can send more than one notification. Label changes and changes to the channel's other properties are sent separately, and a category change can be followed by a label change that Twitch makes. Each notification contains all of the channel's values as of when Twitch prepared it and doesn't say which property changed.
/// </para>
/// </remarks>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.update";

    /// <inheritdoc/>
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
    /// The channel's stream title. Is an empty string if the broadcaster hasn't set a title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The channel's broadcast language, as a lowercase language code. This is usually an ISO 639-1 two-letter code, such as en, but can also be asl, zh-hk, or other. Is an empty string if no broadcast language is set.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// The ID of the channel's category. Is an empty string if the channel has no category, for example after the broadcaster removes it.
    /// </summary>
    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }

    /// <summary>
    /// The name of the channel's category. Is an empty string if the channel has no category.
    /// </summary>
    [JsonPropertyName("category_name")]
    public string? CategoryName { get; init; }

    /// <summary>
    /// The channel's content classification label IDs.
    /// </summary>
    [JsonPropertyName("content_classification_labels")]
    public IReadOnlyList<ContentClassificationLabel.LabelId>? ContentClassificationLabels { get; init; }
}
