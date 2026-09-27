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

namespace StreamActions.Twitch.EventSub.Channel.Moderate;

/// <summary>
/// Metadata associated with an unban request.
/// </summary>
public sealed record UnbanRequest
{
    /// <summary>
    /// Whether or not the unban request was approved or denied.
    /// </summary>
    [JsonPropertyName("is_approved")]
    public bool? IsApproved { get; init; }

    /// <summary>
    /// The ID of the banned user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The login of the user.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user name of the user.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The message included by the moderator explaining their approval or denial.
    /// </summary>
    [JsonPropertyName("moderator_message")]
    public string? ModeratorMessage { get; init; }
}
