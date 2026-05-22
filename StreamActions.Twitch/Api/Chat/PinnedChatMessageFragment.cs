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

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// A fragment of a pinned chat message.
/// </summary>
public sealed record PinnedChatMessageFragment
{
    /// <summary>
    /// The fragment type.
    /// </summary>
    [JsonPropertyName("type")]
    public FragmentTypes? Type { get; init; }

    /// <summary>
    /// Fragment text.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Cheermote metadata. Null if not a cheermote fragment.
    /// </summary>
    [JsonPropertyName("cheermote")]
    public PinnedChatMessageCheermote? Cheermote { get; init; }

    /// <summary>
    /// Emote metadata. Null if not an emote fragment.
    /// </summary>
    [JsonPropertyName("emote")]
    public PinnedChatMessageEmote? Emote { get; init; }

    /// <summary>
    /// Mention metadata. Null if not a mention fragment.
    /// </summary>
    [JsonPropertyName("mention")]
    public PinnedChatMessageMention? Mention { get; init; }

    /// <summary>
    /// The fragment type.
    /// </summary>
    [JsonConverter(typeof(JsonLowerCaseEnumConverter<FragmentTypes>))]
    public enum FragmentTypes
    {
        /// <summary>
        /// Text.
        /// </summary>
        Text,
        /// <summary>
        /// Emote.
        /// </summary>
        Emote,
        /// <summary>
        /// Cheermote.
        /// </summary>
        Cheermote,
        /// <summary>
        /// Mention.
        /// </summary>
        Mention
    }
}
