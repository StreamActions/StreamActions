/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Channels;

/// <summary>
/// The parameters for <see cref="Channel.ModifyChannelInformation(Common.TwitchSession, ModifyChannelParameters)"/>.
/// </summary>
public sealed record ModifyChannelParameters
{
    /// <summary>
    /// The ID of the game that the user plays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game is not updated if the ID isn't a game ID that Twitch recognizes. To unset this field, use <c>"0"</c> or <c>""</c> (an empty string).
    /// </para>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("game_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GameId { get; init; }

    /// <summary>
    /// The user's preferred language.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set the value to an ISO 639-1 two-letter language code (for example, <c>"en"</c> for English). Set to <c>"other"</c> if the user's preferred language is not a Twitch supported language.
    /// </para>
    /// <para>
    /// The language isn't updated if the language code isn't a Twitch supported language.
    /// </para>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("broadcaster_language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BroadcasterLanguage { get; init; }

    /// <summary>
    /// The title of the user's stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You may not set this field to an empty string.
    /// </para>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// The number of seconds you want your broadcast buffered before streaming it live. The delay helps ensure fairness during competitive play.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only users with Partner status may set this field. The maximum delay is 900 seconds (15 minutes).
    /// </para>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("delay")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Delay { get; init; }

    /// <summary>
    /// A list of channel-defined tags to apply to the channel. To remove all tags from the channel, set tags to an empty array. Tags help identify the content that the channel streams.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A channel may specify a maximum of 10 tags. Each tag is limited to a maximum of 25 characters and may not be an empty string or contain spaces or special characters. Tags are case insensitive. For readability, consider using camelCasing or PascalCasing.
    /// </para>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Tags { get; init; }
}
