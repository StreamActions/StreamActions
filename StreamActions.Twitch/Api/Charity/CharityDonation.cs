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
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Charity;

/// <summary>
/// Represents a donation that a user has made to a broadcaster's active charity campaign.
/// </summary>
public sealed record CharityDonation
{
    /// <summary>
    /// An ID that identifies the donation. The ID is unique across campaigns.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that identifies the charity campaign that the donation applies to.
    /// </summary>
    [JsonPropertyName("campaign_id")]
    public string? CampaignId { get; init; }

    /// <summary>
    /// An ID that identifies a user that donated money to the campaign.
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
    /// An object that contains the amount of money that the user donated.
    /// </summary>
    [JsonPropertyName("amount")]
    public CharityAmount? Amount { get; init; }

    /// <summary>
    /// Gets the list of donations that users have made to the broadcaster's active charity campaign.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that's currently running a charity campaign.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="CharityDonation"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; or <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; or the token is not valid.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadCharity"/>.</exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// To receive events as donations occur, subscribe to the <em>channel.charity_campaign.donate</em> subscription type.
    /// </para>
    /// <para>
    /// HTTP Response Status Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of donations that users contributed to the broadcaster's charity campaign.</description>
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
    public static async Task<ResponseData<CharityDonation>?> GetCharityCampaignDonations(TwitchSession session, string broadcasterId, int first = 20, string? after = null)
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

        first = Math.Clamp(first, 1, 100);

        NameValueCollection queryParameters = new()
        {
            { "broadcaster_id", broadcasterId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/charity/donations"), queryParameters);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<CharityDonation>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
