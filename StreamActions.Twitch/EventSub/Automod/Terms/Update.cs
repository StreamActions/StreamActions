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
/// A notification sent when a term is added to or removed from the broadcaster's AutoMod blocked terms or permitted terms list.
/// </summary>
/// <remarks>
/// <para>
/// Changes to private terms are not sent. Adding a term that's on the other list moves it to the new list; you receive only the add notification, not a remove notification for the list the term left. If the broadcaster removes a blocked term without logging the removal for moderators, no notification is sent.
/// </para>
/// </remarks>
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
    /// The ID of the user who added or removed the term.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the broadcaster or one of the broadcaster's moderators, and may differ from the moderator_user_id in the subscription's condition.
    /// </para>
    /// </remarks>
    [JsonPropertyName("moderator_user_id")]
    public string? ModeratorUserId { get; init; }

    /// <summary>
    /// The login of the user who added or removed the term.
    /// </summary>
    [JsonPropertyName("moderator_user_login")]
    public string? ModeratorUserLogin { get; init; }

    /// <summary>
    /// The display name of the user who added or removed the term.
    /// </summary>
    [JsonPropertyName("moderator_user_name")]
    public string? ModeratorUserName { get; init; }

    /// <summary>
    /// The status change applied to the terms.
    /// </summary>
    [JsonPropertyName("action")]
    public TermAction? Action { get; init; }

    /// <summary>
    /// Indicates whether this term was added or removed due to an AutoMod message approve/deny action.
    /// </summary>
    [JsonPropertyName("from_automod")]
    public bool? FromAutomod { get; init; }

    /// <summary>
    /// The list of terms that had a status change.
    /// </summary>
    [JsonPropertyName("terms")]
    public IReadOnlyList<string>? Terms { get; init; }

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
}
