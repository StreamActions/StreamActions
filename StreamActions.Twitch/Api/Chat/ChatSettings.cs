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

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamActions.Common.Exceptions;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Represents the chat settings for a broadcaster's channel.
/// </summary>
public record ChatSettings
{
    /// <summary>
    /// The ID of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// A Boolean value that determines whether chat messages must contain only emotes.
    /// </summary>
    [JsonPropertyName("emote_mode")]
    public bool? EmoteMode { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcaster restricts the chat room to followers only.
    /// </summary>
    [JsonPropertyName("follower_mode")]
    public bool? FollowerMode { get; init; }

    /// <summary>
    /// The length of time, in minutes, that users must follow the broadcaster before being able to participate in the chat room.
    /// </summary>
    [JsonPropertyName("follower_mode_duration")]
    public int? FollowerModeDuration { get; init; }

    /// <summary>
    /// The moderator's ID.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcaster adds a short delay before chat messages appear in the chat room.
    /// </summary>
    [JsonPropertyName("non_moderator_chat_delay")]
    public bool? NonModeratorChatDelay { get; init; }

    /// <summary>
    /// The amount of time, in seconds, that messages are delayed before appearing in chat.
    /// </summary>
    [JsonPropertyName("non_moderator_chat_delay_duration")]
    public int? NonModeratorChatDelayDuration { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcaster limits how often users in the chat room are allowed to send messages.
    /// </summary>
    [JsonPropertyName("slow_mode")]
    public bool? SlowMode { get; init; }

    /// <summary>
    /// The amount of time, in seconds, that users must wait between sending messages.
    /// </summary>
    [JsonPropertyName("slow_mode_wait_time")]
    public int? SlowModeWaitTime { get; init; }

    /// <summary>
    /// A Boolean value that determines whether only users that subscribe to the broadcaster's channel may talk in the chat room.
    /// </summary>
    [JsonPropertyName("subscriber_mode")]
    public bool? SubscriberMode { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcaster requires users to post only unique messages in the chat room.
    /// </summary>
    [JsonPropertyName("unique_chat_mode")]
    public bool? UniqueChatMode { get; init; }

    /// <summary>
    /// Gets the broadcaster's chat settings.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose chat settings you want to get.</param>
    /// <param name="moderatorId">The ID of the broadcaster or one of the broadcaster's moderators.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChatSettings"/> containing the response.</returns>
    /// <remarks>
    /// <para>
    /// HTTP Response Status Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's chat settings.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The <c>broadcaster_id</c> query parameter is required.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The Authorization header is required and must specify a valid app access token or user access token.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ChatSettings>?> GetChatSettings(TwitchSession session, string broadcasterId, string? moderatorId = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorReadChatSettings, Scope.ModeratorManageChatSettings);

        var query = new List<KeyValuePair<string, string>> { new("broadcaster_id", broadcasterId) };
        if (!string.IsNullOrWhiteSpace(moderatorId))
        {
            query.Add(new("moderator_id", moderatorId));
        }

        using var response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new Uri($"/chat/settings?{await new FormUrlEncodedContent(query).ReadAsStringAsync()}", UriKind.Relative), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChatSettings>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the broadcaster's chat settings.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose chat settings you want to update.</param>
    /// <param name="moderatorId">The ID of a user that has permission to moderate the broadcaster's chat room, or the broadcaster's ID if they're making the update.</param>
    /// <param name="settings">The chat settings to update.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChatSettings"/> containing the response.</returns>
    /// <remarks>
    /// <para>
    /// HTTP Response Status Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the broadcaster's chat settings.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>A parameter is missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The Authorization header is required and must specify a user access token.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>The user in <c>moderator_id</c> must have moderator privileges in the broadcaster's channel.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ChatSettings>?> UpdateChatSettings(TwitchSession session, string broadcasterId, string moderatorId, ChatSettings settings)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }
        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ModeratorManageChatSettings);

        var query = new List<KeyValuePair<string, string>>
        {
            new("broadcaster_id", broadcasterId),
            new("moderator_id", moderatorId)
        };

        using var content = JsonContent.Create(settings, options: TwitchApi.SerializerOptions);
        using var response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, new Uri($"/chat/settings?{await new FormUrlEncodedContent(query).ReadAsStringAsync()}", UriKind.Relative), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChatSettings>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
