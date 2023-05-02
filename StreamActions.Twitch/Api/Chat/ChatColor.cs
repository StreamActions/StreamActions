/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Text.Json.Serialization;
using StreamActions.Common.Extensions;
using System.Globalization;
using System.Collections.Specialized;
using System.Drawing;

namespace StreamActions.Twitch.Api.Chat;
public sealed record ChatColor
{
    /// <summary>
    /// An ID that uniquely identifies the user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The Hex color code that the user uses in chat for their name, as a string.
    /// </summary>
    /// <remarks>
    /// If the user hasn't specified a color in their settings, the string is empty.
    /// </remarks>
    [JsonPropertyName("color")]
    public string? ColorString { get; init; }

    /// <summary>
    /// The color that the user uses in chat for their name.
    /// </summary>
    /// <remarks>
    /// If the user hasn't specified a color in their settings, this returns <see langword="null"/>.
    /// </remarks>
    public Color? Color => Util.IsValidHexColor(this.ColorString ?? "") ? Util.HexColorToColor(this.ColorString ?? "") : null;

    /// <summary>
    /// Gets the color used for the user's name in chat.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">The ID of the user whose username color you want to get. Limit: 100.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Chatter"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="userId"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the chat color used by the specified users.</description>
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
    public static async Task<ResponseData<ChatColor>?> GetUserChatColor(TwitchSession session, IEnumerable<string> userId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (userId is null || !userId.Any())
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        if (userId.Count() > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), userId.Count(), "must have a count <= 100").Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        Uri uri = Util.BuildUri(new("/chat/color"), new() { { "user_id", userId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChatColor>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
