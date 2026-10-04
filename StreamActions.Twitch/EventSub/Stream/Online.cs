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
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Stream;

/// <summary>
/// A notification sent when the specified broadcaster starts a stream.
/// </summary>
public sealed record Online : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "stream.online";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The id of the stream.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The broadcaster's user id.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster's user login.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster's user display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The stream type.
    /// </summary>
    [JsonPropertyName("type")]
    public StreamType? TypeOfStream { get; init; }

    /// <summary>
    /// The timestamp at which the stream went online at.
    /// </summary>
    [JsonPropertyName("started_at")]
    public string? StartedAt { get; init; }

    /// <summary>
    /// The type of stream.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<StreamType>))]
    public enum StreamType
    {
        /// <summary>
        /// Live.
        /// </summary>
        [JsonCustomEnum("live")]
        Live,

        /// <summary>
        /// Playlist.
        /// </summary>
        [JsonCustomEnum("playlist")]
        Playlist,

        /// <summary>
        /// Watch party.
        /// </summary>
        [JsonCustomEnum("watch_party")]
        WatchParty,

        /// <summary>
        /// Premiere.
        /// </summary>
        [JsonCustomEnum("premiere")]
        Premiere,

        /// <summary>
        /// Rerun.
        /// </summary>
        [JsonCustomEnum("rerun")]
        Rerun
    }
}
