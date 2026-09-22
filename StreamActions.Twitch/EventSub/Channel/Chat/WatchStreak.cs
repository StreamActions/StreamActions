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

namespace StreamActions.Twitch.EventSub.Channel.Chat;

/// <summary>
/// Information about the Watch Streak event.
/// </summary>
public sealed record WatchStreak
{
    /// <summary>
    /// The number of consecutive broadcasts for which the user has been watching.
    /// </summary>
    [JsonPropertyName("streak_count")]
    public int? StreakCount { get; init; }

    /// <summary>
    /// The number of channel points awarded for the Watch Streak milestone.
    /// </summary>
    [JsonPropertyName("channel_points_awarded")]
    public int? ChannelPointsAwarded { get; init; }
}
