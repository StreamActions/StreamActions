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
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Sends and represents a response element from a request for custom power-ups.
/// </summary>
public sealed record CustomPowerUp
{
    /// <summary>
    /// The ID that uniquely identifies the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The ID that uniquely identifies this custom Power-up.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The title of the custom Power-up.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The prompt shown to the viewer when they redeem the custom Power-up if user input is required.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>
    /// The amount of Bits for the custom Power-up.
    /// </summary>
    [JsonPropertyName("bits")]
    public int? Bits { get; init; }

    /// <summary>
    /// A set of custom images for the custom Power-up. This field is null if the broadcaster didn't upload images.
    /// </summary>
    [JsonPropertyName("image")]
    public Images? Image { get; init; }

    /// <summary>
    /// A set of default images for the custom Power-up.
    /// </summary>
    [JsonPropertyName("default_image")]
    public Images? DefaultImage { get; init; }

    /// <summary>
    /// The background color to use for the custom Power-up. The color is in Hex format (for example, #00E5CB).
    /// </summary>
    [JsonPropertyName("background_color")]
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the custom Power-up is enabled. Is true if enabled; otherwise, false. Disabled custom Power-ups aren't shown to the user.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the user must enter information when redeeming the custom Power-up. Is true if the user is prompted.
    /// </summary>
    [JsonPropertyName("is_user_input_required")]
    public bool? IsUserInputRequired { get; init; }

    /// <summary>
    /// The settings used to determine whether to apply a maximum to the number of redemptions allowed per live stream.
    /// </summary>
    [JsonPropertyName("max_per_stream_setting")]
    public CustomPowerUpMaxPerStreamSetting? MaxPerStreamSetting { get; init; }

    /// <summary>
    /// The settings used to determine whether to apply a maximum to the number of redemptions allowed per user per live stream.
    /// </summary>
    [JsonPropertyName("max_per_user_per_stream_setting")]
    public CustomPowerUpMaxPerUserPerStreamSetting? MaxPerUserPerStreamSetting { get; init; }

    /// <summary>
    /// The settings used to determine whether to apply a cooldown period between redemptions and the length of the cooldown.
    /// </summary>
    [JsonPropertyName("global_cooldown_setting")]
    public CustomPowerUpGlobalCooldownSetting? GlobalCooldownSetting { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the custom Power-up is currently paused. Is true if the custom Power-up is paused. Viewers can't redeem paused custom Power-ups.
    /// </summary>
    [JsonPropertyName("is_paused")]
    public bool? IsPaused { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the custom Power-up is currently in stock. Is true if the custom Power-up is in stock. Viewers can't redeem out of stock custom Power-ups.
    /// </summary>
    [JsonPropertyName("is_in_stock")]
    public bool? IsInStock { get; init; }

    /// <summary>
    /// The number of redemptions redeemed during the current live stream. The number counts against the <see cref="MaxPerStreamSetting"/> limit. This field is null if the broadcaster's stream isn't live or <see cref="MaxPerStreamSetting"/> isn't enabled.
    /// </summary>
    [JsonPropertyName("redemptions_redeemed_current_stream")]
    public int? RedemptionsRedeemedCurrentStream { get; init; }

    /// <summary>
    /// The timestamp of when the cooldown period expires. Is null if the custom Power-up isn't in a cooldown state.
    /// </summary>
    [JsonPropertyName("cooldown_expires_at")]
    public DateTime? CooldownExpiresAt { get; init; }

    /// <summary>
    /// Gets a list of custom Power-ups that the specified broadcaster created.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose custom Power-ups you want to get. This ID must match the user ID found in the access token.</param>
    /// <param name="id">A list of IDs to filter the Power-ups by. You may specify a maximum of 50 IDs.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="CustomPowerUp"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not <see langword="null"/> and has more than 50 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.BitsRead"/>.</exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's list of custom Power-ups.</description>
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
    /// <term>403 Forbidden</term>
    /// <description>The broadcaster is not a partner or affiliate.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>All of the custom Power-ups specified using the <paramref name="id"/> parameter were not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<CustomPowerUp>?> GetCustomPowerUps(TwitchSession session, string broadcasterId, IEnumerable<string>? id = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (id is not null && id.Count() > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 50").Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.BitsRead);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId }
        };

        if (id is not null && id.Any())
        {
            queryParams.Add("id", id);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/bits/custom_power_ups"), queryParams), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<CustomPowerUp>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
