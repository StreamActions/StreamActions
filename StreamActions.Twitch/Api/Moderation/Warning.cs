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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents a warning issued to a user in a broadcaster's chat room.
/// </summary>
public sealed record Warning
{
    /// <summary>
    /// The ID of the channel in which the warning will take effect.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The ID of the warned user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The ID of the user who applied the warning.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// The reason provided for warning.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Warns a user in the specified broadcaster's chat room, preventing them from chat interaction until the warning is acknowledged.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the channel in which the warning will take effect.</param>
    /// <param name="moderatorId">The ID of the twitch user who requested the warning.</param>
    /// <param name="parameters">The <see cref="WarnChatUserParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Warning"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><see cref="WarnChatUserData.UserId"/> or <see cref="WarnChatUserData.Reason"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="WarnChatUserData.Reason"/> contains more than 500 characters.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageWarnings"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// If the <see cref="TwitchToken.OAuth"/> in <paramref name="session"/> is an <see cref="TwitchToken.TokenType.App"/> token, this endpoint additionally requires the app to have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageWarnings"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully warned a user.</description>
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
    /// <description>The user in <paramref name="moderatorId"/> is not one of the broadcaster's moderators.</description>
    /// </item>
    /// <item>
    /// <term>409 Conflict</term>
    /// <description>You may not update the user's warning state while someone else is updating the state.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<Warning>?> WarnChatUser(TwitchSession session, string broadcasterId, string moderatorId, WarnChatUserParameters parameters)
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

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Data is null)
        {
            throw new ArgumentNullException(nameof(WarnChatUserParameters.Data)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Data.UserId))
        {
            throw new ArgumentNullException(nameof(WarnChatUserData.UserId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Data.Reason))
        {
            throw new ArgumentNullException(nameof(WarnChatUserData.Reason)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Data.Reason.Length > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(WarnChatUserData.Reason), parameters.Data.Reason.Length, "length must be <= 500").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageWarnings);

        Uri uri = Util.BuildUri(new("/moderation/warnings"), new() { { "broadcaster_id", broadcasterId }, { "moderator_id", moderatorId } });
        using JsonContent jsonContent = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Warning>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
