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
using StreamActions.Common.Net;

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.GuestStar;

/// <summary>
/// The layout of guests within a session in the browser source.
/// </summary>
[JsonConverter(typeof(JsonCustomEnumConverter<GuestStarGroupLayout>))]
public enum GuestStarGroupLayout
{
    /// <summary>
    /// All live guests are tiled within the browser source with the same size.
    /// </summary>
    [JsonCustomEnum("TILED_LAYOUT")]
    TiledLayout,

    /// <summary>
    /// All live guests are tiled within the browser source with the same size. If there is an active screen share, it is sized larger than the other guests.
    /// </summary>
    [JsonCustomEnum("SCREENSHARE_LAYOUT")]
    ScreenshareLayout,

    /// <summary>
    /// All live guests are arranged in a horizontal bar within the browser source
    /// </summary>
    [JsonCustomEnum("HORIZONTAL_LAYOUT")]
    HorizontalLayout,

    /// <summary>
    /// All live guests are arranged in a vertical bar within the browser source
    /// </summary>
    [JsonCustomEnum("VERTICAL_LAYOUT")]
    VerticalLayout
}

/// <summary>
/// Represents the channel settings for configuration of the Guest Star feature for a particular host.
/// </summary>
public sealed record ChannelGuestStarSettings
{
    /// <summary>
    /// Flag determining if Guest Star moderators have access to control whether a guest is live once assigned to a slot.
    /// </summary>
    [JsonPropertyName("is_moderator_send_live_enabled")]
    public bool? IsModeratorSendLiveEnabled { get; init; }

    /// <summary>
    /// Number of slots the Guest Star call interface will allow the host to add to a call. Required to be between 1 and 6.
    /// </summary>
    [JsonPropertyName("slot_count")]
    public int? SlotCount { get; init; }

    /// <summary>
    /// Flag determining if Browser Sources subscribed to sessions on this channel should output audio.
    /// </summary>
    [JsonPropertyName("is_browser_source_audio_enabled")]
    public bool? IsBrowserSourceAudioEnabled { get; init; }

    /// <summary>
    /// This setting determines how the guests within a session should be laid out within the browser source.
    /// </summary>
    [JsonPropertyName("group_layout")]
    public GuestStarGroupLayout? GroupLayout { get; init; }

    /// <summary>
    /// View only token to generate browser source URLs.
    /// </summary>
    [JsonPropertyName("browser_source_token")]
    public string? BrowserSourceToken { get; init; }

    /// <summary>
    /// Gets the channel settings for configuration of the Guest Star feature for a particular host.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster you want to get guest star settings for.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelGuestStarSettings"/> containing the response, or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the settings.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing broadcaster_id or moderator_id.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>Insufficient authorization for viewing channel's Guest Star settings.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ChannelGuestStarSettings>?> GetChannelGuestStarSettings(TwitchSession session, string broadcasterId, string moderatorId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelReadGuestStar, Scope.ChannelManageGuestStar, Scope.ModeratorReadGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/guest_star/channel_settings"), queryParams), session).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ResponseData<ChannelGuestStarSettings>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }

        JsonApiResponse? error = await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        TwitchApi.GetLogger().Warning($"Failed to get channel guest star settings: {response.StatusCode} {error?.Message}");

        return null;
    }

    /// <summary>
    /// Mutates the channel settings for configuration of the Guest Star feature for a particular host.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster you want to update Guest Star settings for.</param>
    /// <param name="parameters">The parameters to update.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully updated channel settings.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing broadcaster_id, invalid slot_count, or invalid group_layout.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> UpdateChannelGuestStarSettings(TwitchSession session, string broadcasterId, ChannelGuestStarSettingsUpdateParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId }
        };

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, Util.BuildUri(new("/guest_star/channel_settings"), queryParams), session, content).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new JsonApiResponse { Status = response.StatusCode };
        }

        JsonApiResponse? error = await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        TwitchApi.GetLogger().Warning($"Failed to update channel guest star settings: {response.StatusCode} {error?.Message}");

        return error ?? new JsonApiResponse { Status = response.StatusCode, Message = "Unknown error" };
    }
}
