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

namespace StreamActions.Twitch.EventSub.User;

/// <summary>
/// A notification sent when user updates their account.
/// </summary>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(ClientIdCondition);

    /// <inheritdoc/>
    public static string Type => "user.update";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The user's user id.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's user login.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user's user display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The user's email address. The event includes the user's email address only if the app used to request this event type includes the user:read:email scope for the user; otherwise, the field is set to an empty string.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    /// A Boolean value that determines whether Twitch has verified the user's email address. Is true if Twitch has verified the email address; otherwise, false. NOTE: Ignore this field if the email field contains an empty string.
    /// </summary>
    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; init; }

    /// <summary>
    /// The user's description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
