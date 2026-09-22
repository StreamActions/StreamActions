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

namespace StreamActions.Twitch.EventSub.Channel.Bits;

/// <summary>
/// Data about a default (i.e. built-in) Power-up.
/// </summary>
public sealed record PowerUp
{
    /// <summary>
    /// Possible values: message_effect, celebration, gigantify_an_emote.
    /// </summary>
    [JsonPropertyName("type")]
    public PowerUpType? Type { get; init; }

    /// <summary>
    /// Optional. Emote associated with the reward.
    /// </summary>
    [JsonPropertyName("emote")]
    public Emote? Emote { get; init; }

    /// <summary>
    /// Optional. The ID of the message effect.
    /// </summary>
    [JsonPropertyName("message_effect_id")]
    public string? MessageEffectId { get; init; }

    /// <summary>
    /// The type of the power-up.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<PowerUpType>))]
    public enum PowerUpType
    {
        /// <summary>
        /// A message effect.
        /// </summary>
        [JsonCustomEnum("message_effect")]
        MessageEffect,

        /// <summary>
        /// A celebration.
        /// </summary>
        [JsonCustomEnum("celebration")]
        Celebration,

        /// <summary>
        /// A gigantified emote.
        /// </summary>
        [JsonCustomEnum("gigantify_an_emote")]
        GigantifyAnEmote
    }
}
