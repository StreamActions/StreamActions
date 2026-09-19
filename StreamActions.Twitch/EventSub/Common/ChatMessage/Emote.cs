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

namespace StreamActions.Twitch.EventSub.Common.ChatMessage;

/// <summary>
/// Metadata pertaining to the emote.
/// </summary>
public sealed record Emote
{
    /// <summary>
    /// An ID that uniquely identifies this emote.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that identifies the emote set that the emote belongs to.
    /// </summary>
    [JsonPropertyName("emote_set_id")]
    public string? EmoteSetId { get; init; }

    /// <summary>
    /// The ID of the broadcaster who owns the emote.
    /// </summary>
    [JsonPropertyName("owner_id")]
    public string? OwnerId { get; init; }

    /// <summary>
    /// The formats that the emote is available in.
    /// </summary>
    [JsonPropertyName("format")]
    public IEnumerable<Formats>? Format { get; init; }

    /// <summary>
    /// The formats that an emote can be available in.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<Formats>))]
    public enum Formats
    {
        /// <summary>
        /// An animated GIF is available for this emote.
        /// </summary>
        [JsonCustomEnum("animated")]
        Animated,

        /// <summary>
        /// A static PNG file is available for this emote.
        /// </summary>
        [JsonCustomEnum("static")]
        Static
    }
}
