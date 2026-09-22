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

namespace StreamActions.Twitch.EventSub.Channel.UnbanRequest;

/// <summary>
/// An event that is sent when an unban request has been resolved.
/// </summary>
public sealed record Resolve : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.unban_request.resolve";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The ID of the unban request.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The broadcasters user ID for the channel the unban request was updated for.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcasters login name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcasters display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// Optional. User ID of moderator who approved/denied the request.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// Optional. The moderators login name
    /// </summary>
    [JsonPropertyName("moderator_login")]
    public string? ModeratorLogin { get; init; }

    /// <summary>
    /// Optional. The moderators display name
    /// </summary>
    [JsonPropertyName("moderator_name")]
    public string? ModeratorName { get; init; }

    /// <summary>
    /// User ID of user that requested to be unbanned.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The users login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The users display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// Optional. Resolution text supplied by the mod/broadcaster upon approval/denial of the request.
    /// </summary>
    [JsonPropertyName("resolution_text")]
    public string? ResolutionText { get; init; }

    /// <summary>
    /// Dictates whether the unban request was approved or denied. Can be the following: approved canceled denied
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
