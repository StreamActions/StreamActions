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
using StreamActions.Common.Exceptions;
using StreamActions.Common.Extensions;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Common.Logger;
using System.Collections.Specialized;
using System.Net.Http.Json;

namespace StreamActions.Twitch.Api.Extensions;

/// <summary>
/// Contains extension chat message methods.
/// </summary>
public sealed record ExtensionChatMessage
{
    /// <summary>
    /// Sends a message to the specified broadcaster’s chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Requires a signed JSON Web Token (JWT) created by an Extension Backend Service (EBS).</param>
    /// <param name="broadcasterId">The ID of the broadcaster that has activated the extension.</param>
    /// <param name="parameters">The <see cref="SendExtensionChatMessageParameters"/> for the request.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.Jwt"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully sent the chat message.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The JWT token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> SendExtensionChatMessage(TwitchSession session, string broadcasterId, SendExtensionChatMessageParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireJwtToken();

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId }
        };

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/extensions/chat"), queryParams), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
