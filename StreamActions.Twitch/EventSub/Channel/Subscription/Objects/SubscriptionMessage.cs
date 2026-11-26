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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.Subscription.Objects;

/// <summary>
/// An object that contains the resubscription message and emote information needed to recreate the message.
/// </summary>
public sealed record SubscriptionMessage
{
    /// <summary>
    /// The text of the resubscription chat message.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// An array that includes the emote ID and start and end positions for where the emote appears in the text.
    /// </summary>
    [JsonPropertyName("emotes")]
    public IReadOnlyList<SubscriptionMessageEmote>? Emotes { get; init; }
}
