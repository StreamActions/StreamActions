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
using System.Drawing;
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Net;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Sends and represents a response element for a request to retrieve or update a users chat color.
/// </summary>
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
    /// The color to use for the user's name in chat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All users may specify one of the named color values.
    /// </para>
    /// <para>
    /// Turbo and Prime users may specify either a named color value or <see cref="Custom"/>.
    /// When using <see cref="Custom"/>, a custom hex value may be specified instead.
    /// </para>
    /// </remarks>
    public enum NamedColor
    {
        [JsonCustomEnum("blue")]
        Blue,
        [JsonCustomEnum("blue_violet")]
        BlueViolet,
        [JsonCustomEnum("cadet_blue")]
        CadetBlue,
        [JsonCustomEnum("chocolate")]
        Chocolate,
        [JsonCustomEnum("coral")]
        Coral,
        [JsonCustomEnum("dodger_blue")]
        DodgerBlue,
        [JsonCustomEnum("firebrick")]
        Firebrick,
        [JsonCustomEnum("golden_rod")]
        GoldenRod,
        [JsonCustomEnum("green")]
        Green,
        [JsonCustomEnum("hot_pink")]
        HotPink,
        [JsonCustomEnum("orange_red")]
        OrangeRed,
        [JsonCustomEnum("red")]
        Red,
        [JsonCustomEnum("sea_green")]
        SeaGreen,
        [JsonCustomEnum("spring_green")]
        SpringGreen,
        [JsonCustomEnum("yellow_green")]
        YellowGreen,
        /// <summary>
        /// A custom color specified in hex.
        /// </summary>
        /// <remarks>
        /// Only Turbo and Prime users may use this value.
        /// </remarks>
        Custom
    }

    /// <summary>
    /// Gets the color used for the user's name in chat.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">The ID of the user whose username color you want to get. Limit: 100.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChatColor"/> containing the response.</returns>
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

    /// <summary>
    /// Updates the color used for the user's name in chat.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">The ID of the user whose chat color you want to update. This ID must match the user ID in the access token.</param>
    /// <param name="color">The color to use for the user's name in chat.</param>
    /// <param name="hex">The hex color code to use for the user's name in chat. Used only if <paramref name="color"/> is set to <see cref="NamedColor.Custom"/>.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="color"/> is not a valid value; <paramref name="color"/> is <see cref="NamedColor.Custom"/> and <paramref name="hex"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hex"/> is not a valid rgb hex string <c>#rrggbb</c>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserManageChatColor"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully updated the user's chat color.</description>
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
    public static async Task<JsonApiResponse?> UpdateUserChatColor(TwitchSession session, string userId, NamedColor color, string? hex = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        if (color == NamedColor.Custom && string.IsNullOrWhiteSpace(hex))
        {
            throw new ArgumentNullException(nameof(hex)).Log(TwitchApi.GetLogger());
        }

        if (color == NamedColor.Custom && !Util.IsValidHexColor(hex!))
        {
            throw new ArgumentOutOfRangeException(nameof(hex), hex, "must be a valid rgb hex string #rrggbb").Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.UserManageChatColor);

        string scolor;

        if (color == NamedColor.Custom)
        {
            scolor = hex!;
        }
        else
        {
            scolor = new JsonCustomEnumConverter<NamedColor>().Convert(color) ?? "";

            if (string.IsNullOrWhiteSpace(scolor))
            {
                throw new ArgumentNullException(nameof(color)).Log(TwitchApi.GetLogger());
            }
        }

        Uri uri = Util.BuildUri(new("/chat/color"), new() { { "user_id", userId }, { "color", scolor } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
