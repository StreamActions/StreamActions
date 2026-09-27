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

namespace StreamActions.Twitch.EventSub.Channel.Moderate;

/// <summary>
/// A moderator performs a moderation action in a channel. Includes warnings.
/// </summary>
public sealed record Moderate : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.moderate";

    /// <inheritdoc/>
    public static string Version => "2";

    /// <summary>
    /// The ID of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The login of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The user name of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The channel in which the action originally occurred. Is the same as the broadcaster_user_id if not in shared chat.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_id")]
    public string? SourceBroadcasterUserId { get; init; }

    /// <summary>
    /// The channel in which the action originally occurred. Is the same as the broadcaster_user_login if not in shared chat.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_login")]
    public string? SourceBroadcasterUserLogin { get; init; }

    /// <summary>
    /// The channel in which the action originally occurred. Is null when the moderator action happens in the same channel as the broadcaster. Is not null when in a shared chat session, and the action happens in the channel of a participant other than the broadcaster.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_name")]
    public string? SourceBroadcasterUserName { get; init; }

    /// <summary>
    /// The ID of the moderator who performed the action.
    /// </summary>
    [JsonPropertyName("moderator_user_id")]
    public string? ModeratorUserId { get; init; }

    /// <summary>
    /// The login of the moderator.
    /// </summary>
    [JsonPropertyName("moderator_user_login")]
    public string? ModeratorUserLogin { get; init; }

    /// <summary>
    /// The user name of the moderator.
    /// </summary>
    [JsonPropertyName("moderator_user_name")]
    public string? ModeratorUserName { get; init; }

    /// <summary>
    /// The action performed.
    /// </summary>
    [JsonPropertyName("action")]
    public string? Action { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the followers command.
    /// </summary>
    [JsonPropertyName("followers")]
    public Followers? Followers { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the slow command.
    /// </summary>
    [JsonPropertyName("slow")]
    public Slow? Slow { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the vip command.
    /// </summary>
    [JsonPropertyName("vip")]
    public Vip? Vip { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the unvip command.
    /// </summary>
    [JsonPropertyName("unvip")]
    public Unvip? Unvip { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the mod command.
    /// </summary>
    [JsonPropertyName("mod")]
    public ModerateMod? Mod { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the unmod command.
    /// </summary>
    [JsonPropertyName("unmod")]
    public Unmod? Unmod { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the ban command.
    /// </summary>
    [JsonPropertyName("ban")]
    public Ban? Ban { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the unban command.
    /// </summary>
    [JsonPropertyName("unban")]
    public Unban? Unban { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the timeout command.
    /// </summary>
    [JsonPropertyName("timeout")]
    public Timeout? Timeout { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the untimeout command.
    /// </summary>
    [JsonPropertyName("untimeout")]
    public Untimeout? Untimeout { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the raid command.
    /// </summary>
    [JsonPropertyName("raid")]
    public Raid? Raid { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the unraid command.
    /// </summary>
    [JsonPropertyName("unraid")]
    public Unraid? Unraid { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the delete command.
    /// </summary>
    [JsonPropertyName("delete")]
    public Delete? Delete { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the automod terms changes.
    /// </summary>
    [JsonPropertyName("automod_terms")]
    public AutomodTerms? AutomodTerms { get; init; }

    /// <summary>
    /// Optional. Metadata associated with an unban request.
    /// </summary>
    [JsonPropertyName("unban_request")]
    public UnbanRequest? UnbanRequest { get; init; }

    /// <summary>
    /// Optional. Metadata associated with the warn command.
    /// </summary>
    [JsonPropertyName("warn")]
    public Warn? Warn { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_ban event. Is null if action is not shared_chat_ban.
    /// </summary>
    [JsonPropertyName("shared_chat_ban")]
    public SharedChatBan? SharedChatBan { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_unban event. Is null if action is not shared_chat_unban.
    /// </summary>
    [JsonPropertyName("shared_chat_unban")]
    public SharedChatUnban? SharedChatUnban { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_timeout event. Is null if action is not shared_chat_timeout.
    /// </summary>
    [JsonPropertyName("shared_chat_timeout")]
    public SharedChatTimeout? SharedChatTimeout { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_untimeout event. Is null if action is not shared_chat_untimeout.
    /// </summary>
    [JsonPropertyName("shared_chat_untimeout")]
    public SharedChatUntimeout? SharedChatUntimeout { get; init; }

    /// <summary>
    /// Optional. Information about the shared_chat_delete event. Is null if action is not shared_chat_delete.
    /// </summary>
    [JsonPropertyName("shared_chat_delete")]
    public SharedChatDelete? SharedChatDelete { get; init; }
}
