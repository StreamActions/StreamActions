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
using StreamActions.Twitch.Api.Common;
using StreamActions.Common.Logger;
using System.Collections.Specialized;
using System.Net.Http.Json;

namespace StreamActions.Twitch.Api.Extensions;

/// <summary>
/// Contains extension bits products methods.
/// </summary>
public sealed record ExtensionBitsProduct
{
    /// <summary>
    /// Gets the list of Bits products that belongs to the extension.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Must be for an App Access Token.</param>
    /// <param name="shouldIncludeAll">A Boolean value that determines whether to include disabled or expired Bits products in the response.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionProductData"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.App"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of products.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The ID in the Client-Id header must belong to an extension.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ExtensionProductData>?> GetExtensionBitsProducts(TwitchSession session, bool shouldIncludeAll = false)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        NameValueCollection queryParams = new();
        if (shouldIncludeAll)
        {
            queryParams.Add("should_include_all", "true");
        }

        Uri uri = Util.BuildUri(new("/bits/extensions"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ExtensionProductData>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds or updates a Bits product that the extension created.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Must be for an App Access Token.</param>
    /// <param name="parameters">The <see cref="UpdateExtensionBitsProductParameters"/> for the request.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionProductData"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.App"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully created the product.</description>
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
    public static async Task<ResponseData<ExtensionProductData>?> UpdateExtensionBitsProduct(TwitchSession session, UpdateExtensionBitsProductParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, new("/bits/extensions"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ExtensionProductData>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
