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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Extensions;

/// <summary>
/// Sends and represents a response element for a request for extension secrets.
/// </summary>
public sealed record ExtensionSecret
{
    /// <summary>
    /// The version number that identifies this definition of the secret's data.
    /// </summary>
    [JsonPropertyName("format_version")]
    public int? FormatVersion { get; init; }

    /// <summary>
    /// The list of secrets.
    /// </summary>
    [JsonPropertyName("secrets")]
    public IReadOnlyList<ExtensionSecretData>? Secrets { get; init; }

    /// <summary>
    /// Gets an extension's list of shared secrets.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Requires a signed JSON Web Token (JWT) created by an Extension Backend Service (EBS).</param>
    /// <param name="extensionId">The ID of the extension whose shared secrets you want to get.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionSecret"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="extensionId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.Jwt"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of secrets.</description>
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
    public static async Task<ResponseData<ExtensionSecret>?> GetExtensionSecrets(TwitchSession session, string extensionId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireJwtToken();

        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentNullException(nameof(extensionId)).Log(TwitchApi.GetLogger());
        }

        NameValueCollection queryParams = new()
        {
            { "extension_id", extensionId }
        };

        Uri uri = Util.BuildUri(new("/extensions/jwt/secrets"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ExtensionSecret>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a shared secret used to sign and verify JWT tokens.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Requires a signed JSON Web Token (JWT) created by an Extension Backend Service (EBS).</param>
    /// <param name="extensionId">The ID of the extension to apply the shared secret to.</param>
    /// <param name="delay">The amount of time, in seconds, to delay activating the secret. Minimum: 300. Default: 300.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionSecret"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="extensionId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.Jwt"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully created the new secret.</description>
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
    public static async Task<ResponseData<ExtensionSecret>?> CreateExtensionSecret(TwitchSession session, string extensionId, int delay = 300)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireJwtToken();

        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentNullException(nameof(extensionId)).Log(TwitchApi.GetLogger());
        }

        NameValueCollection queryParams = new()
        {
            { "extension_id", extensionId },
            { "delay", delay.ToString(CultureInfo.InvariantCulture) }
        };

        Uri uri = Util.BuildUri(new("/extensions/jwt/secrets"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ExtensionSecret>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
