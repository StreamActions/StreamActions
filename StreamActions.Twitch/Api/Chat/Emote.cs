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
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Sends and represents a response element for a request for emotes.
/// </summary>
public sealed record Emote
{
    /// <summary>
    /// An ID that identifies this emote.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The name of the emote. This is the name that viewers type in the chat window to get the emote to appear.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The image URLs for the emote.
    /// </summary>
    [JsonPropertyName("images")]
    public EmoteImage? Images { get; init; }

    /// <summary>
    /// The subscriber tier at which the emote is unlocked. This field contains the tier information only if <see cref="EmoteType"/> is set to <see cref="EmoteTypes.Subscriptions"/>.
    /// </summary>
    /// <remarks>
    /// This field is not present for global emotes.
    /// </remarks>
    [JsonPropertyName("tier")]
    public string? Tier { get; init; }

    /// <summary>
    /// The type of emote. For a list of possible values, see <see cref="EmoteTypes"/>.
    /// </summary>
    /// <remarks>
    /// This field is not present for global emotes.
    /// </remarks>
    [JsonPropertyName("emote_type")]
    public EmoteTypes? EmoteType { get; init; }

    /// <summary>
    /// An ID that identifies the emote set that the emote belongs to.
    /// </summary>
    /// <remarks>
    /// This field is not present for global emotes.
    /// </remarks>
    [JsonPropertyName("emote_set_id")]
    public string? EmoteSetId { get; init; }

    /// <summary>
    /// The formats that the emote is available in.
    /// </summary>
    [JsonPropertyName("format")]
    public Format[]? Format { get; init; }

    /// <summary>
    /// The sizes that the emote is available in.
    /// </summary>
    [JsonPropertyName("scale")]
    public Scale[]? Scale { get; init; }

    /// <summary>
    /// The background themes that the emote is available in.
    /// </summary>
    [JsonPropertyName("theme_mode")]
    public ThemeMode[]? ThemeMode { get; init; }

    /// <summary>
    /// A templated URL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the values from the <see cref="Id"/>, <see cref="Format"/>, <see cref="Scale"/>, and <see cref="ThemeMode"/> fields to replace the like-named placeholder strings in the templated URL to create a CDN URL that you use to fetch the emote.
    /// </para>
    /// <para>
    /// The placeholders are <c>{{id}}</c>, <c>{{format}}</c>, <c>{{scale}}</c>, and <c>{{theme_mode}}</c>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("template")]
    public string? Template { get; init; }

    /// <summary>
    /// The type of emote.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<EmoteTypes>))]
    public enum EmoteTypes
    {
        /// <summary>
        /// A custom Bits tier emote.
        /// </summary>
        [JsonCustomEnum("bitstier")]
        BitsTier,
        /// <summary>
        /// A custom follower emote.
        /// </summary>
        [JsonCustomEnum("follower")]
        Follower,
        /// <summary>
        /// A custom subscriber emote.
        /// </summary>
        [JsonCustomEnum("subscriptions")]
        Subscriptions
    }

    /// <summary>
    /// The formats that the emote is available in.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<Format>))]
    public enum Format
    {
        /// <summary>
        /// An animated GIF is available for this emote.
        /// </summary>
        [JsonCustomEnum("animated")]
        Animated,
        /// <summary>
        /// A static PNG file is available for this emote.
        /// </summary>
        [JsonCustomEnum("static")]
        Static
    }

    /// <summary>
    /// The sizes that the emote is available in.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<Scale>))]
    public enum Scale
    {
        /// <summary>
        /// A small version (28px x 28px) is available.
        /// </summary>
        [JsonCustomEnum("1.0")]
        Small,
        /// <summary>
        /// A medium version (56px x 56px) is available.
        /// </summary>
        [JsonCustomEnum("2.0")]
        Medium,
        /// <summary>
        /// A large version (112px x 112px) is available.
        /// </summary>
        [JsonCustomEnum("3.0")]
        Large
    }

    /// <summary>
    /// The background themes that the emote is available in.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<ThemeMode>))]
    public enum ThemeMode
    {
        /// <summary>
        /// A dark theme is available.
        /// </summary>
        [JsonCustomEnum("dark")]
        Dark,
        /// <summary>
        /// A light theme is available.
        /// </summary>
        [JsonCustomEnum("light")]
        Light
    }

    /// <summary>
    /// Gets the broadcaster's list of custom emotes.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">An ID that identifies the broadcaster whose emotes you want to get.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Emote"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's list of custom emotes.</description>
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
    public static async Task<ResponseData<Emote>?> GetChannelEmotes(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        Uri uri = Util.BuildUri(new("/chat/emotes"), new() { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Emote>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the list of global emotes.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Emote"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved Twitch's list of global emotes.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Emote>?> GetGlobalEmotes(TwitchSession session)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        Uri uri = Util.BuildUri(new("/chat/emotes/global"));
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Emote>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
