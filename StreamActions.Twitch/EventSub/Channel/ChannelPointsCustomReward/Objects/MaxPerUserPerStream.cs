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
using StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomReward.Objects;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomReward.Objects;

/// <summary>
/// Whether a maximum per user per stream is enabled and what the maximum is.
/// </summary>
    [SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Intentionally named to match Twitch API")]
public sealed record MaxPerUserPerStream
{
    /// <summary>
    /// Is the setting enabled.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; init; }

    /// <summary>
    /// The max per user per stream limit.
    /// </summary>
    [JsonPropertyName("value")]
    public int Value { get; init; }
}
