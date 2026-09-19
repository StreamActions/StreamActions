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

namespace StreamActions.Twitch.EventSub.Automod.Message;

/// <summary>
/// Metadata surrounding the potential inappropriate fragments of the message.
/// </summary>
public sealed record Fragment
{
    /// <summary>
    /// One of three options: text, emote, cheermote.
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
        Cheermote
    }
}
