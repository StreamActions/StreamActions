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
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Charity;

/// <summary>
/// Represents a charity campaign that a broadcaster is running.
/// </summary>
public sealed record CharityCampaign
{
    /// <summary>
    /// An ID that identifies the charity campaign.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that identifies the broadcaster that's running the campaign.
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
    /// The charity's name.
    /// </summary>
    [JsonPropertyName("charity_name")]
    public string? CharityName { get; init; }

    /// <summary>
    /// A description of the charity.
    /// </summary>
    [JsonPropertyName("charity_description")]
    public string? CharityDescription { get; init; }

    /// <summary>
    /// A URL to an image of the charity's logo.
    /// </summary>
    [JsonPropertyName("charity_logo")]
    public Uri? CharityLogo { get; init; }

    /// <summary>
    /// A URL to the charity's website.
    /// </summary>
    [JsonPropertyName("charity_website")]
    public Uri? CharityWebsite { get; init; }

    /// <summary>
    /// The current amount of donations that the campaign has received.
    /// </summary>
    [JsonPropertyName("current_amount")]
    public CharityAmount? CurrentAmount { get; init; }

    /// <summary>
    /// The campaign's fundraising goal. This field is null if the broadcaster has not defined a fundraising goal.
    /// </summary>
    [JsonPropertyName("target_amount")]
    public CharityAmount? TargetAmount { get; init; }

    /// <summary>
    /// Gets information about the charity campaign that a broadcaster is running.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that's currently running a charity campaign.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="CharityCampaign"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; or <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; or the token is not valid.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadCharity"/>.</exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// To receive events when progress is made towards the campaign's goal or the broadcaster changes the fundraising goal, subscribe to the <em>channel.charity_campaign.progress</em> subscription type.
    /// </para>
    /// <para>
    /// HTTP Response Status Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved information about the broadcaster's active charity campaign.</description>
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
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<CharityCampaign>?> GetCharityCampaigns(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelReadCharity);

        Uri uri = Util.BuildUri(new("/charity/campaigns"), new() { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<CharityCampaign>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
