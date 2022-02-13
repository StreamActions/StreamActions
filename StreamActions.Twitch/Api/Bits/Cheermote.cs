﻿/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using StreamActions.Common;
using StreamActions.Twitch.Api.Common;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits
{
    /// <summary>
    /// Sends and represents a response for a request for a list of cheermotes.
    /// </summary>
    public record Cheermote
    {
        /// <summary>
        /// The string used to Cheer that precedes the Bits amount.
        /// </summary>
        [JsonPropertyName("prefix")]
        public string? Prefix { get; init; }

        /// <summary>
        /// A list of <see cref="CheermoteTier"/> with their metadata.
        /// </summary>
        [JsonPropertyName("tiers")]
        public IReadOnlyList<CheermoteTier>? Tiers { get; init; }

        /// <summary>
        /// Shows the type of emote.
        /// </summary>
        [JsonPropertyName("type")]
        public CheermoteType? Type { get; init; }

        /// <summary>
        /// Order of the emotes as shown in the bits card, in ascending order.
        /// </summary>
        [JsonPropertyName("order")]
        public int? Order { get; init; }

        /// <summary>
        /// The date when this Cheermote was last updated.
        /// </summary>
        [JsonPropertyName("last_updated")]
        public DateTime? LastUpdated { get; init; }

        /// <summary>
        /// Indicates whether or not this emote provides a charity contribution match during charity campaigns.
        /// </summary>
        [JsonPropertyName("is_charitable")]
        public bool? IsCharitable { get; init; }

        /// <summary>
        /// The types of cheermotes.
        /// </summary>
        public enum CheermoteType
        {
            /// <summary>
            /// A global cheermote that belongs to Twitch.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
            Global_first_party,
            /// <summary>
            /// A global cheermote that belongs to a third-party company.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
            Global_third_party,
            /// <summary>
            /// A custom cheermote uploaded by the broadcaster.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
            Channel_custom,
            /// <summary>
            /// A display-only cheermote.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
            Display_only,
            /// <summary>
            /// A sponsored cheermote.
            /// </summary>
            Sponsored
        }

        /// <summary>
        /// Retrieves the list of available Cheermotes, animated emotes to which viewers can assign Bits, to cheer in chat. Cheermotes returned are available throughout Twitch, in all Bits-enabled channels.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="broadcasterId">ID for the broadcaster who might own specialized Cheermotes.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Cheermote"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
        public static async Task<ResponseData<Cheermote>?> GetCheermotes(TwitchSession session, string? broadcasterId = null)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Dictionary<string, IEnumerable<string>> queryParams = new() { };

            if (!string.IsNullOrWhiteSpace(broadcasterId))
            {
                queryParams.Add("broadcaster_id", new List<string> { broadcasterId });
            }

            Uri uri = Util.BuildUri(new("/bits/cheermotes"), queryParams);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<Cheermote>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }
    }
}
