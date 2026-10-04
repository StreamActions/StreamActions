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
using StreamActions.Twitch.EventSub.User.Whisper.Objects;

namespace StreamActions.Twitch.EventSub.User.Whisper;

/// <summary>
/// A notification sent when a user receives a whisper.
/// </summary>
public sealed record Message : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(UserIdCondition);

    /// <inheritdoc/>
    public static string Type => "user.whisper.message";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The ID of the user sending the message.
    /// </summary>
    [JsonPropertyName("from_user_id")]
    public string? FromUserId { get; init; }

    /// <summary>
    /// The name of the user sending the message.
    /// </summary>
    [JsonPropertyName("from_user_name")]
    public string? FromUserName { get; init; }

    /// <summary>
    /// The login of the user sending the message.
    /// </summary>
    [JsonPropertyName("from_user_login")]
    public string? FromUserLogin { get; init; }

    /// <summary>
    /// The ID of the user receiving the message.
    /// </summary>
    [JsonPropertyName("to_user_id")]
    public string? ToUserId { get; init; }

    /// <summary>
    /// The name of the user receiving the message.
    /// </summary>
    [JsonPropertyName("to_user_name")]
    public string? ToUserName { get; init; }

    /// <summary>
    /// The login of the user receiving the message.
    /// </summary>
    [JsonPropertyName("to_user_login")]
    public string? ToUserLogin { get; init; }

    /// <summary>
    /// The whisper ID.
    /// </summary>
    [JsonPropertyName("whisper_id")]
    public string? WhisperId { get; init; }

    /// <summary>
    /// Object containing whisper information.
    /// </summary>
    [JsonPropertyName("whisper")]
    public WhisperMessage? Whisper { get; init; }
}
