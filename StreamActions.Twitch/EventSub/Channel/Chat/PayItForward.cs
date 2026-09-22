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
/// Information about the pay it forward event.
/// </summary>
public sealed record PayItForward
{
    /// <summary>
    /// Whether the gift was given anonymously.
    /// </summary>
    [JsonPropertyName("gifter_is_anonymous")]
    public bool? GifterIsAnonymous { get; init; }

    /// <summary>
    /// The user ID of the user who gifted the subscription. Null if anonymous.
    /// </summary>
    [JsonPropertyName("gifter_user_id")]
    public string? GifterUserId { get; init; }

    /// <summary>
    /// Optional. The user name of the user who gifted the subscription. Null if anonymous.
    /// </summary>
    [JsonPropertyName("gifter_user_name")]
    public string? GifterUserName { get; init; }

    /// <summary>
    /// The user login of the user who gifted the subscription. Null if anonymous.
    /// </summary>
    [JsonPropertyName("gifter_user_login")]
    public string? GifterUserLogin { get; init; }
}
