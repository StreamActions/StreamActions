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
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Users;

/// <summary>
/// Represents the active extensions that the broadcaster has installed.
/// </summary>
public sealed record UserActiveExtensions
{
    /// <summary>
    /// A dictionary that contains the data for a panel extension.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dictionary's key is a sequential number beginning with 1.
    /// </para>
    /// </remarks>
    [JsonPropertyName("panel")]
    public IReadOnlyDictionary<string, UserActiveExtension>? Panel { get; init; }

    /// <summary>
    /// A dictionary that contains the data for a video-overlay extension.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dictionary's key is a sequential number beginning with 1.
    /// </para>
    /// </remarks>
    [JsonPropertyName("overlay")]
    public IReadOnlyDictionary<string, UserActiveExtension>? Overlay { get; init; }

    /// <summary>
    /// A dictionary that contains the data for a video-component extension.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dictionary's key is a sequential number beginning with 1.
    /// </para>
    /// </remarks>
    [JsonPropertyName("component")]
    public IReadOnlyDictionary<string, UserActiveComponentExtension>? Component { get; init; }

    /// <summary>
    /// Gets the active extensions that the broadcaster has installed for each configuration.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">The ID of the broadcaster whose active extensions you want to get.</param>
    /// <returns>A <see cref="UserActiveExtensionsResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> or <see cref="TwitchToken.TokenType.App"/> token.</exception>
    /// <remarks>
    /// <para>
    /// The <paramref name="userId"/> parameter is required if you specify an app access token and is optional if you specify a user access token. If you specify a user access token and don't specify this parameter, the API uses the user ID from the access token.
    /// </para>
    /// <para>
    /// To include extensions that you have under development, you must specify a user access token that includes the <see cref="Scope.UserReadBroadcast"/> or <see cref="Scope.UserEditBroadcast"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the user's active extensions.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<UserActiveExtensionsResponse?> GetUserActiveExtensions(TwitchSession session, string? userId = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParams = [];
        if (!string.IsNullOrWhiteSpace(userId))
        {
            queryParams.Add("user_id", userId);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/users/extensions"), queryParams), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<UserActiveExtensionsResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates an installed extension's information.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The extensions to update.</param>
    /// <returns>A <see cref="UserActiveExtensionsResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserEditBroadcast"/>.</exception>
    /// <remarks>
    /// <para>
    /// You can update the extension's activation state, ID, and version number. The user ID in the access token identifies the broadcaster whose extensions you're updating.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the active extensions.</description>
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
    /// <term>404 Not Found</term>
    /// <description>An extension with the specified id and version values was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<UserActiveExtensionsResponse?> UpdateUserExtensions(TwitchSession session, UpdateUserExtensionsParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.UserEditBroadcast);

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, new("/users/extensions"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<UserActiveExtensionsResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
