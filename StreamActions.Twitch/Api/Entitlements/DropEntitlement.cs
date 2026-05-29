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
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Entitlements;

/// <summary>
/// Represents a Drop entitlement.
/// </summary>
public sealed record DropEntitlement
{
    /// <summary>
    /// An ID that identifies the entitlement.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that identifies the benefit (reward).
    /// </summary>
    [JsonPropertyName("benefit_id")]
    public string? BenefitId { get; init; }

    /// <summary>
    /// The UTC date and time of when the entitlement was granted.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }

    /// <summary>
    /// An ID that identifies the user who was granted the entitlement.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// An ID that identifies the game the user was playing when the reward was entitled.
    /// </summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; init; }

    /// <summary>
    /// The entitlement's fulfillment status.
    /// </summary>
    [JsonPropertyName("fulfillment_status")]
    public FulfillmentStatuses? FulfillmentStatus { get; init; }

    /// <summary>
    /// The UTC date and time of when the entitlement was last updated.
    /// </summary>
    [JsonPropertyName("last_updated")]
    public DateTime? LastUpdated { get; init; }

    /// <summary>
    /// The fulfillment status of a Drop entitlement.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<FulfillmentStatuses>))]
    public enum FulfillmentStatuses
    {
        /// <summary>
        /// The user claimed the benefit.
        /// </summary>
        [JsonCustomEnum("CLAIMED")]
        Claimed,
        /// <summary>
        /// The developer granted the benefit that the user claimed.
        /// </summary>
        [JsonCustomEnum("FULFILLED")]
        Fulfilled
    }

    /// <summary>
    /// Gets an organization’s list of entitlements that have been granted to a game, a user, or both.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="ids">An ID that identifies the entitlement to get. You may specify a maximum of 100 IDs.</param>
    /// <param name="userId">An ID that identifies a user that was granted entitlements.</param>
    /// <param name="gameId">An ID that identifies a game that offered entitlements.</param>
    /// <param name="fulfillmentStatus">The entitlement’s fulfillment status. Used to filter the list to only those with the specified status.</param>
    /// <param name="first">The maximum number of entitlements to return per page in the response. Minimum: 1. Maximum: 1000. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="DropEntitlement"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ids"/> has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; or the token is not valid.</exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token when using a user access token and not providing a <paramref name="userId"/> or <paramref name="gameId"/>.
    /// </para>
    /// <para>
    /// HTTP Response Status Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the entitlements.</description>
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
    public static async Task<ResponseData<DropEntitlement>?> GetDropsEntitlements(TwitchSession session, IEnumerable<string>? ids = null, string? userId = null, string? gameId = null, FulfillmentStatuses? fulfillmentStatus = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParameters = [];

        if (ids is not null)
        {
            List<string> idsList = ids.ToList();
            if (idsList.Count > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(ids), idsList.Count, "must have a count <= 100").Log(TwitchApi.GetLogger());
            }
            queryParameters.Add("id", idsList);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            queryParameters.Add("user_id", userId);
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            queryParameters.Add("game_id", gameId);
        }

        if (fulfillmentStatus.HasValue)
        {
            queryParameters.Add("fulfillment_status", new JsonCustomEnumConverter<FulfillmentStatuses>().Convert(fulfillmentStatus.Value) ?? "");
        }

        first = Math.Clamp(first, 1, 1000);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/entitlements/drops"), queryParameters);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<DropEntitlement>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the Drop entitlement’s fulfillment status.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="UpdateDropsEntitlementsParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="UpdateDropsEntitlementsStatus"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; or the token is not valid.</exception>
    /// <remarks>
    /// <para>
    /// HTTP Response Status Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully requested the updates. Check the response to determine which updates succeeded.</description>
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
    public static async Task<ResponseData<UpdateDropsEntitlementsStatus>?> UpdateDropsEntitlements(TwitchSession session, UpdateDropsEntitlementsParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, Util.BuildUri(new("/entitlements/drops")), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<UpdateDropsEntitlementsStatus>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
