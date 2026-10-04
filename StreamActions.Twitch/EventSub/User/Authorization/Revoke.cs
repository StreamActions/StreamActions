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

namespace StreamActions.Twitch.EventSub.User.Authorization;

/// <summary>
/// A notification sent when a user revokes authorization for your application.
/// </summary>
public sealed record Revoke : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(ClientIdCondition);

    /// <inheritdoc/>
    public static string Type => "user.authorization.revoke";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The client_id of the application with revoked user access.
    /// </summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    /// <summary>
    /// The user id for the user who has revoked authorization for your client id.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user login for the user who has revoked authorization for your client id. This is null if the user no longer exists.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user display name for the user who has revoked authorization for your client id. This is null if the user no longer exists.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }
}
