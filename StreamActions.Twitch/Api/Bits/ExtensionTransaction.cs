/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Sends and represents the response for a request for extension transactions.
/// </summary>
public sealed record ExtensionTransaction
{
    /// <summary>
    /// An ID that identifies the transaction.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The UTC date and time of the transaction.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }

    /// <summary>
    /// The ID of the broadcaster that owns the channel where the transaction occurred.
    /// </summary>
    [JsonPropertyName("bradcaster_id")]
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
    /// The ID of the user that purchased the digital product.
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
    /// The type of transaction.
    /// </summary>
    [JsonPropertyName("product_type")]
    public ExtensionProductType? ProductType { get; init; }

    /// <summary>
    /// Contains details about the digital product.
    /// </summary>
    [JsonPropertyName("product_data")]
    public ExtensionProductData? ProductData { get; init; }

    /// <summary>
    /// The type of transaction.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<ExtensionProductType>))]
    public enum ExtensionProductType
    {
        /// <summary>
        /// A product which is purchased via bits in an extension.
        /// </summary>
        [JsonCustomEnum("BITS_IN_EXTENSION")]
        BitsInExtension
    }

    /// <summary>
    /// Gets an extension's list of transactions. A transaction records the exchange of a currency (for example, Bits) for a digital product.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Must be for an App Access Token.</param>
    /// <param name="extensionId">The ID of the extension whose list of transactions you want to get.</param>
    /// <param name="id">A transaction ID used to filter the list of transactions. Maximum: 100.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of result.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionTransaction"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="extensionId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not <see langword="null"/> and contains more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="after"/> is specified at the same time as a valid value in <paramref name="id"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.App"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of transactions.</description>
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
    /// <term>404 Not Found</term>
    /// <description>The specified transaction id does not exist.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ExtensionTransaction>?> GetExtensionTransactions(TwitchSession session, string extensionId, IEnumerable<string>? id = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentNullException(nameof(extensionId)).Log(TwitchApi.GetLogger());
        }

        if (id is not null && id.Count() > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 100").Log(TwitchApi.GetLogger());
        }

        first = Math.Clamp(first, 1, 100);

        NameValueCollection queryParams = new()
        {
            { "extension_id", extensionId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (id is not null && id.Any())
        {
            List<string> ids = [];
            foreach (string transactionId in id)
            {
                if (!string.IsNullOrWhiteSpace(transactionId))
                {
                    ids.Add(transactionId);
                }
            }

            if (ids.Count != 0)
            {
                queryParams.Add("id", ids);
            }
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            if (queryParams.AllKeys.Contains("id"))
            {
                throw new InvalidOperationException("Can not use " + nameof(after) + " at the same time as " + nameof(id)).Log(TwitchApi.GetLogger());
            }
            else
            {
                queryParams.Add("after", after);
            }
        }

        Uri uri = Util.BuildUri(new("/extensions/transactions"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ExtensionTransaction>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
