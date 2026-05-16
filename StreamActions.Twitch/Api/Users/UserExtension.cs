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

using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Users;

/// <summary>
/// Represents an extension that the broadcaster has installed.
/// </summary>
public sealed record UserExtension
{
    /// <summary>
    /// The extension types that you can activate for this extension.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<UserExtensionTypes>))]
    public enum UserExtensionTypes
    {
        /// <summary>
        /// Component extension.
        /// </summary>
        [JsonCustomEnum("component")]
        Component,
        /// <summary>
        /// Mobile extension.
        /// </summary>
        [JsonCustomEnum("mobile")]
        Mobile,
        /// <summary>
        /// Overlay extension.
        /// </summary>
        [JsonCustomEnum("overlay")]
        Overlay,
        /// <summary>
        /// Panel extension.
        /// </summary>
        [JsonCustomEnum("panel")]
        Panel
    }

    /// <summary>
    /// An ID that identifies the extension.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The extension's version.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// The extension's name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the extension is configured and can be activated.
    /// </summary>
    [JsonPropertyName("can_activate")]
    public bool? CanActivate { get; init; }

    /// <summary>
    /// The extension types that you can activate for this extension.
    /// </summary>
    [JsonPropertyName("type")]
    public IReadOnlyList<UserExtensionTypes>? Type { get; init; }

    /// <summary>
    /// Gets a list of all extensions (both active and inactive) that the broadcaster has installed.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="UserExtension"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserReadBroadcast"/> or <see cref="Scope.UserEditBroadcast"/>.</exception>
    /// <remarks>
    /// <para>
    /// The user ID in the access token identifies the broadcaster.
    /// </para>
    /// <para>
    /// To include inactive extensions, you must include the <see cref="Scope.UserEditBroadcast"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the user's installed extensions.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<UserExtension>?> GetUserExtensions(TwitchSession session)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.UserReadBroadcast, Scope.UserEditBroadcast);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new("/users/extensions/list"), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<UserExtension>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
