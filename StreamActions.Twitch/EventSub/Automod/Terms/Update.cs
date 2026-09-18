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

using StreamActions.Common.Json.Serialization;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Automod.Terms;

/// <summary>
/// A notification is sent when a broadcasters automod terms are updated. Changes to private terms are not sent.
/// </summary>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "automod.terms.update";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The ID of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The login of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The user name of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The ID of the moderator who changed the channel settings.
    /// </summary>
    [JsonPropertyName("moderator_user_id")]
    public string? ModeratorUserId { get; init; }

    /// <summary>
    /// The moderators login.
    /// </summary>
    [JsonPropertyName("moderator_user_login")]
    public string? ModeratorUserLogin { get; init; }

    /// <summary>
    /// The moderators user name.
    /// </summary>
    [JsonPropertyName("moderator_user_name")]
    public string? ModeratorUserName { get; init; }

    /// <summary>
    /// The status change applied to the terms. Possible options are: add_permitted, remove_permitted, add_blocked, remove_blocked.
    /// </summary>
    [JsonPropertyName("action")]
    public TermAction? Action { get; init; }

    /// <summary>
    /// Indicates whether this term was added due to an Automod message approve/deny action.
    /// </summary>
    [JsonPropertyName("from_automod")]
    public bool? FromAutomod { get; init; }

    /// <summary>
    /// The list of terms that had a status change.
    /// </summary>
    [JsonPropertyName("terms")]
    public IEnumerable<string>? Terms { get; init; }
}

/// <summary>
/// The status change applied to the terms.
/// </summary>
[JsonConverter(typeof(JsonCustomEnumConverter<TermAction>))]
public enum TermAction
{
    /// <summary>
    /// A term was added to the permitted list.
    /// </summary>
    [JsonCustomEnum("add_permitted")]
    AddPermitted,

    /// <summary>
    /// A term was removed from the permitted list.
    /// </summary>
    [JsonCustomEnum("remove_permitted")]
    RemovePermitted,

    /// <summary>
    /// A term was added to the blocked list.
    /// </summary>
    [JsonCustomEnum("add_blocked")]
    AddBlocked,

    /// <summary>
    /// A term was removed from the blocked list.
    /// </summary>
    [JsonCustomEnum("remove_blocked")]
    RemoveBlocked
}
