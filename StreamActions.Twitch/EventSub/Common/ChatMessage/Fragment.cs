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
/// A fragment of a chat message.
/// </summary>
public sealed record Fragment
{
    /// <summary>
    /// The type of message fragment.
    /// </summary>
    [JsonPropertyName("type")]
    public FragmentType? Type { get; init; }

    /// <summary>
    /// Message text in a fragment.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Optional. Metadata pertaining to the emote.
    /// </summary>
    [JsonPropertyName("emote")]
    public Emote? Emote { get; init; }

    /// <summary>
    /// Optional. Metadata pertaining to the cheermote.
    /// </summary>
    [JsonPropertyName("cheermote")]
    public Cheermote? Cheermote { get; init; }

    /// <summary>
    /// Optional. Metadata pertaining to the mention.
    /// </summary>
    [JsonPropertyName("mention")]
    public Mention? Mention { get; init; }

    /// <summary>
    /// Optional. Metadata pertaining to the GIF.
    /// </summary>
    [JsonPropertyName("gif")]
    public Gif? Gif { get; init; }

    /// <summary>
    /// The type of the fragment.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<FragmentType>))]
    public enum FragmentType
    {
        /// <summary>
        /// A text fragment.
        /// </summary>
        [JsonCustomEnum("text")]
        Text,

        /// <summary>
        /// An emote fragment.
        /// </summary>
        [JsonCustomEnum("emote")]
        Emote,

        /// <summary>
        /// A cheermote fragment.
        /// </summary>
        [JsonCustomEnum("cheermote")]
        Cheermote,

        /// <summary>
        /// A mention fragment.
        /// </summary>
        [JsonCustomEnum("mention")]
        Mention,

        /// <summary>
        /// A gif fragment.
        /// </summary>
        [JsonCustomEnum("gif")]
        Gif
    }
}
