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

using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.GuestStarSession;

/// <summary>
/// A Guest Star session begins.
/// </summary>
public sealed record SessionBegin : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.guest_star_session.begin";

    /// <inheritdoc/>
    public static string Version => "beta";

    /// <summary>
    /// The ID of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The user name of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The login of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// ID associated with the session.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    /// RFC3339 timestamp indicating the time the session began.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }
}
