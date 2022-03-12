/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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
using StreamActions.Twitch.Api.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits
{
    /// <summary>
    /// Sends and represents the response for a request for extension transactions.
    /// </summary>
    public record ExtensionTransaction
    {
        /// <summary>
        /// Unique identifier of the Bits-in-Extensions transaction.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        /// <summary>
        /// UTC timestamp when this transaction occurred.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; init; }

        /// <summary>
        /// Twitch user ID of the channel the transaction occurred on.
        /// </summary>
        [JsonPropertyName("bradcaster_id")]
        public string? BroadcasterId { get; init; }

        /// <summary>
        /// Login name of the broadcaster.
        /// </summary>
        [JsonPropertyName("broadcaster_login")]
        public string? BroadcasterLogin { get; init; }

        /// <summary>
        /// Twitch display name of the broadcaster.
        /// </summary>
        [JsonPropertyName("broadcaster_name")]
        public string? BroadcasterName { get; init; }

        /// <summary>
        /// Twitch user ID of the user who generated the transaction.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string? UserId { get; init; }

        /// <summary>
        /// Login name of the user who generated the transaction.
        /// </summary>
        [JsonPropertyName("user_login")]
        public string? UserLogin { get; init; }

        /// <summary>
        /// Twitch display name of the user who generated the transaction.
        /// </summary>
        [JsonPropertyName("user_name")]
        public string? UserName { get; init; }

        /// <summary>
        /// Enum of the product type.
        /// </summary>
        [JsonPropertyName("product_type")]
        public ExtensionProductType? ProductType { get; init; }

        /// <summary>
        /// A <see cref="ExtensionProductData"/> that represents the product acquired, as it looked at the time of the transaction.
        /// </summary>
        [JsonPropertyName("product_data")]
        public ExtensionProductData? ProductData { get; init; }

        /// <summary>
        /// The types of extension products.
        /// </summary>
        public enum ExtensionProductType
        {
            /// <summary>
            /// A product which is purchased via bits in an extension.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
            BITS_IN_EXTENSION
        }

        /// <summary>
        /// Gets the list of Extension transactions for a given Extension. This allows Extension back-end servers to fetch a list of transactions that have occurred for their Extension across all of Twitch. A transaction is a record of a user exchanging Bits for an in-Extension digital good.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Must be for an App Access Token.</param>
        /// <param name="extensionId">ID of the Extension to list transactions for.</param>
        /// <param name="id">Transaction IDs to look up. Can include multiple to fetch multiple transactions in a single request. Maximum: 100.</param>
        /// <param name="after">The cursor used to fetch the next page of data. This only applies to queries without ID.</param>
        /// <param name="first">Maximum number of objects to return. Maximum: 100. Default: 20.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionTransaction"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="extensionId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="id"/> is not null and contains more than 100 elements; <paramref name="after"/> is specified at the same time as a valid value in <paramref name="id"/>.</exception>
        public static async Task<ResponseData<ExtensionTransaction>?> GetExtensionTransactions(TwitchSession session, string extensionId, IEnumerable<string>? id = null, string? after = null, int first = 20)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(extensionId))
            {
                throw new ArgumentNullException(nameof(extensionId));
            }

            if (id is not null && id.Count() > 100)
            {
                throw new InvalidOperationException(nameof(id) + " must have a count <= 100");
            }

            first = Math.Clamp(first, 1, 100);

            Dictionary<string, IEnumerable<string>> queryParams = new()
            {
                { "extension_id", new List<string> { extensionId } },
                { "first", new List<string> { first.ToString(CultureInfo.InvariantCulture) } }
            };

            if (id is not null && id.Any())
            {
                List<string> ids = new();
                foreach (string transactionId in id)
                {
                    if (!string.IsNullOrWhiteSpace(transactionId))
                    {
                        ids.Add(transactionId);
                    }
                }

                if (ids.Any())
                {
                    queryParams.Add("id", ids);
                }
            }

            if (!string.IsNullOrWhiteSpace(after))
            {
                if (queryParams.ContainsKey("id"))
                {
                    throw new InvalidOperationException("Can not use " + nameof(after) + " at the same time as " + nameof(id));
                }
                else
                {
                    queryParams.Add("after", new List<string> { after });
                }
            }

            Uri uri = Util.BuildUri(new("/extensions/transactions"), queryParams);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<ExtensionTransaction>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }
    }
}
