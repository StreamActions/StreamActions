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
    /// <remarks>
    /// <para>
    /// These image URLs always provide a static, non-animated emote image with a light background.
    /// </para>
    /// <para>
    /// You should use the templated URL in the <see cref="Emote.Template"/> field to fetch the image instead of using these URLs.
    /// </para>
    /// </remarks>
    [JsonPropertyName("images")]
    public EmoteImages? Images { get; init; }

    /// <summary>
    /// The subscriber tier at which the emote is unlocked.
    /// </summary>
    /// <remarks>
    /// This field contains the tier information only if <see cref="EmoteType"/> is set to <see cref="EmoteTypes.Subscriptions"/>.
    /// <para>This field is not present in the <see cref="GetGlobalEmotes"/> or <see cref="GetUserEmotes"/> APIs.</para>
    /// </remarks>
    [JsonPropertyName("tier")]
    public string? Tier { get; init; }

    /// <summary>
    /// The type of emote. For a list of possible values, see <see cref="EmoteTypes"/>.
    /// </summary>
    /// <remarks>
    /// This field is not present in the <see cref="GetGlobalEmotes"/> API.
    /// </remarks>
    [JsonPropertyName("emote_type")]
    public EmoteTypes? EmoteType { get; init; }

    /// <summary>
    /// An ID that identifies the emote set that the emote belongs to.
    /// </summary>
    /// <remarks>
    /// This field is not present in the <see cref="GetGlobalEmotes"/> API.
    /// <para>This may be blank for some global emotes, such as those with an <see cref="EmoteType"/> of <see cref="EmoteTypes.Hypetrain"/>.</para>
    /// </remarks>
    [JsonPropertyName("emote_set_id")]
    public string? EmoteSetId { get; init; }

    /// <summary>
    /// The ID of the broadcaster who owns the emote.
    /// </summary>
    /// <remarks>This field is only present in the <see cref="GetEmoteSets"/> and <see cref="GetUserEmotes"/> APIs.</remarks>
    [JsonPropertyName("owner_id")]
    public string? OwnerId { get; init; }

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
        /// No emote type was assigned to this emote.
        /// </summary>
        [JsonCustomEnum("none")]
        None,
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
        Subscriptions,
        /// <summary>
        /// An emote granted by using channel points.
        /// </summary>
        [JsonCustomEnum("channelpoints")]
        ChannelPoints,
        /// <summary>
        /// An emote granted to the user through a special event.
        /// </summary>
        [JsonCustomEnum("rewards")]
        Rewards,
        /// <summary>
        /// An emote granted for participation in a Hype Train.
        /// </summary>
        [JsonCustomEnum("hypetrain")]
        HypeTrain,
        /// <summary>
        /// An emote granted for linking an Amazon Prime account.
        /// </summary>
        [JsonCustomEnum("prime")]
        Prime,
        /// <summary>
        /// An emote granted for having Twitch Turbo.
        /// </summary>
        [JsonCustomEnum("turbo")]
        Turbo,
        /// <summary>
        /// Emoticons supported by Twitch.
        /// </summary>
        [JsonCustomEnum("smilies")]
        Smilies,
        /// <summary>
        /// An emote accessible by everyone.
        /// </summary>
        [JsonCustomEnum("globals")]
        Globals,
        /// <summary>
        /// Emotes related to Overwatch League 2019.
        /// </summary>
        [JsonCustomEnum("owl2019")]
        OWL2019,
        /// <summary>
        /// Emotes granted by enabling two-factor authentication on an account.
        /// </summary>
        [JsonCustomEnum("twofactor")]
        TwoFactor,
        /// <summary>
        /// Emotes that were granted for only a limited time.
        /// </summary>
        [JsonCustomEnum("limitedtime")]
        LimitedTime
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

    /// <summary>
    /// Gets emotes for one or more specified emote sets.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="emoteSetIds">An ID that identifies the emote set to get. You may specify a maximum of 25 IDs.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Emote"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="emoteSetIds"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Too many emote set ids were specified.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the emotes for the specified emote sets.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The emote_set_id query parameter is required; or the number of emote_set_id query parameters exceeds the maximum allowed.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Emote>?> GetEmoteSets(TwitchSession session, IEnumerable<string> emoteSetIds)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (emoteSetIds is null || !emoteSetIds.Any())
        {
            throw new ArgumentNullException(nameof(emoteSetIds)).Log(TwitchApi.GetLogger());
        }

        if (emoteSetIds.Count() > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(emoteSetIds), emoteSetIds.Count(), "A maximum of 25 emote set ids can be specified.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        System.Collections.Specialized.NameValueCollection query = new();
        foreach (string emoteSetId in emoteSetIds)
        {
            query.Add("emote_set_id", emoteSetId);
        }

        Uri uri = Util.BuildUri(new("/chat/emotes/set"), query);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Emote>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves emotes available to the user across all channels.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">The ID of the user. This ID must match the user ID in the user access token.</param>
    /// <param name="broadcasterId">The User ID of a broadcaster you wish to get follower emotes of.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Emote"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException">The user token is missing the required 'user:read:emotes' scope.</exception>
    /// <remarks>
    /// <para>
    /// If the user specified in <paramref name="userId"/> is subscribed to the broadcaster specified in <paramref name="broadcasterId"/>, their follower emotes will appear in the response body regardless if this query parameter is used.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the emotes.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The user_id query parameter is required; or the ID in the user_id query parameter is not valid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Emote>?> GetUserEmotes(TwitchSession session, string userId, string? broadcasterId = null, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken("user:read:emotes");

        System.Collections.Specialized.NameValueCollection query = new() { { "user_id", userId } };

        if (!string.IsNullOrWhiteSpace(broadcasterId))
        {
            query.Add("broadcaster_id", broadcasterId);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            query.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/chat/emotes/user"), query);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Emote>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
