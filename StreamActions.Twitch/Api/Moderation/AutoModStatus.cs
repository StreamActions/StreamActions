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

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents the result of an AutoMod status check.
/// </summary>
public sealed record AutoModStatus
{
    /// <summary>
    /// The caller-defined ID passed in the request.
    /// </summary>
    [JsonPropertyName("msg_id")]
    public string? MsgId { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether Twitch would approve the message for chat.
    /// </summary>
    [JsonPropertyName("is_permitted")]
    public bool? IsPermitted { get; init; }

    /// <summary>
    /// Checks whether AutoMod would flag the specified message for review.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose AutoMod settings and list of blocked terms are used to check the message.</param>
    /// <param name="parameters">The <see cref="CheckAutoModStatusParameters"/> containing the messages to check.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="AutoModStatus"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentException"><paramref name="parameters"/> data is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameters"/> data contains more than 100 messages.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModerationRead"/>.</exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from <paramref name="broadcasterId"/> which includes the <see cref="Scope.ModerationRead"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully checked the messages.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>The ID in <paramref name="broadcasterId"/> must match the user ID in the user access token.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description>The broadcaster exceeded the number of chat message checks that they may make.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<AutoModStatus>?> CheckAutoModStatus(TwitchSession session, string broadcasterId, CheckAutoModStatusParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Data is null || !parameters.Data.Any())
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        int count = parameters.Data.Count();
        if (count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), count, "The list must contain at most 100 messages.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModerationRead);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);

        using JsonContent jsonContent = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/moderation/enforcements/status"), queryParameters), session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<AutoModStatus>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Allow or deny the message that AutoMod flagged for review.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="ManageHeldAutoModMessageParameters"/> containing the action details.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <see cref="ManageHeldAutoModMessageParameters.UserId"/>, <see cref="ManageHeldAutoModMessageParameters.MsgId"/> is <see langword="null"/>, empty, or whitespace; <see cref="ManageHeldAutoModMessageParameters.Action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageAutomod"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, the ID in <paramref name="parameters"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from the user in <paramref name="parameters"/> which includes the <see cref="Scope.ModeratorManageAutomod"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully approved or denied the message.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>The user in <paramref name="parameters"/> is not one of the broadcaster's moderators.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The message specified in the <paramref name="parameters"/> was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> ManageHeldAutoModMessage(TwitchSession session, ManageHeldAutoModMessageParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.UserId))
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.MsgId))
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Action is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageAutomod);

        using JsonContent jsonContent = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/moderation/automod/message")), session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
