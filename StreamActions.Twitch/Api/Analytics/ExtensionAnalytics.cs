/*
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
using StreamActions.Common.Exceptions;
using StreamActions.Twitch.Api.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Analytics
{
    /// <summary>
    /// Sends and represents a response element for a request for extension analytics.
    /// </summary>
    public record ExtensionAnalytics
    {
        /// <summary>
        /// ID of the extension whose analytics data is being provided.
        /// </summary>
        [JsonPropertyName("extension_id")]
        public string? ExtensionId { get; init; }

        /// <summary>
        /// URL to the downloadable CSV file containing analytics data. Valid for 5 minutes.
        /// </summary>
        [JsonPropertyName("URL")]
        public Uri? Url { get; init; }

        /// <summary>
        /// Type of report.
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        /// <summary>
        /// The date range covered by the report.
        /// </summary>
        [JsonPropertyName("date_range")]
        public DateRange? DateRange { get; init; }

        /// <summary>
        /// Gets a URL that Extension developers can use to download analytics reports (CSV files) for their Extensions. The URL is valid for 5 minutes.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="extensionId">Client ID value assigned to the extension when it is created. If this is specified, the returned URL points to an analytics report for just the specified extension. If this is not specified, the response includes multiple URLs (paginated), pointing to separate analytics reports for each of the authenticated user's Extensions.</param>
        /// <param name="after">Cursor for forward pagination: tells the server where to start fetching the next set of results, in a multi-page response. This applies only to queries without <paramref name="extensionId"/>.</param>
        /// <param name="first">Maximum number of objects to return. Maximum: 100. Default: 20.</param>
        /// <param name="startedAt">Starting date/time for returned reports. This must be on or after January 31, 2018.</param>
        /// <param name="endedAt">Ending date/time for returned reports. The report covers the entire ending date. This must be on or after January 31, 2018.</param>
        /// <param name="type">Type of analytics report that is returned. Currently, this field has no affect on the response as there is only one report type. If additional types were added, using this field would return only the URL for the specified report. Valid values: <c>overview_v2</c>.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionAnalytics"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
        /// <exception cref="ScopeMissingException"><paramref name="session"/> contains a scope list, and <c>analytics:read:extensions</c> is not present.</exception>
        /// <exception cref="InvalidOperationException">Specified only one of <paramref name="startedAt"/>/<paramref name="endedAt"/> without specifying the other.</exception>
        public static async Task<ResponseData<ExtensionAnalytics>?> GetExtensionAnalytics(TwitchSession session, string? extensionId = null, string? after = null, int first = 20, DateTime? startedAt = null, DateTime? endedAt = null,
            string type = "overview_v2")
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (!session.Token?.HasScope("analytics:read:extensions") ?? false)
            {
                throw new ScopeMissingException("analytics:read:extensions");
            }

            first = Math.Clamp(first, 1, 100);

            Dictionary<string, IEnumerable<string>> queryParams = new()
            {
                { "first", new List<string> { first.ToString(CultureInfo.InvariantCulture) } },
                { "type", new List<string> { type } }
            };

            if (!string.IsNullOrWhiteSpace(extensionId))
            {
                queryParams.Add("extension_id", new List<string> { extensionId });
            }

            if (!string.IsNullOrWhiteSpace(after))
            {
                queryParams.Add("after", new List<string> { after });
            }

            if ((!startedAt.HasValue && endedAt.HasValue) || (startedAt.HasValue && !endedAt.HasValue))
            {
                throw new InvalidOperationException("If " + nameof(startedAt) + " is specified, " + nameof(endedAt) + " must also be specified (and vice-versa)");
            }

            if (startedAt.HasValue && endedAt.HasValue)
            {
                queryParams.Add("started_at", new List<string> { startedAt.Value.Date.ToRfc3339() });
                queryParams.Add("ended_at", new List<string> { endedAt.Value.Date.ToRfc3339() });
            }

            Uri uri = Util.BuildUri(new("/analytics/extensions"), queryParams);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<ExtensionAnalytics>>().ConfigureAwait(false);
        }
    }
}
