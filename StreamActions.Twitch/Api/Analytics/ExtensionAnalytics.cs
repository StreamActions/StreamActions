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
using StreamActions.Common.Exceptions;
using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Analytics;

/// <summary>
/// Sends and represents a response element for a request for extension analytics.
/// </summary>
public sealed record ExtensionAnalytics
{
    /// <summary>
    /// An ID that identifies the extension that the report was generated for.
    /// </summary>
    [JsonPropertyName("extension_id")]
    public string? ExtensionId { get; init; }

    /// <summary>
    /// The URL that you use to download the report. The URL is valid for 5 minutes.
    /// </summary>
    [JsonPropertyName("URL")]
    public Uri? Url { get; init; }

    /// <summary>
    /// The type of report.
    /// </summary>
    [JsonPropertyName("type")]
    public ExtensionReportType? Type { get; init; }

    /// <summary>
    /// The reporting window's start and end dates.
    /// </summary>
    [JsonPropertyName("date_range")]
    public DateRange? DateRange { get; init; }

    /// <summary>
    /// Extension report types.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<ExtensionReportType>))]
    public enum ExtensionReportType
    {
        /// <summary>
        /// <c>overview_v2</c> report.
        /// </summary>
        [JsonCustomEnum("overview_v2")]
        OverviewV2
    }

    /// <summary>
    /// Gets an analytics report for one or more extensions. The response contains the URLs used to download the reports (CSV files).
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="extensionId">The extension's client ID. If specified, the response contains a report for the specified extension. If not specified, the response includes a report for each extension that the authenticated user owns.</param>
    /// <param name="type">The type of analytics report to get.</param>
    /// <param name="startedAt">The reporting window's start date. This must be on or after January 31, 2018. If you specify a start date, you must specify an end date.</param>
    /// <param name="endedAt">The reporting window's end date. This must be on or after January 31, 2018. Because it can take up to two days for the data to be available, you must specify an end date that's earlier than today minus one to two days. If not, the API ignores your end date and uses an end date that is today minus one to two days.</param>
    /// <param name="first">The maximum number of report URLs to return per page in the response. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionAnalytics"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.AnalyticsReadExtensions"/>.</exception>
    /// <exception cref="InvalidOperationException">Specified only one of <paramref name="startedAt"/>/<paramref name="endedAt"/> without specifying the other.</exception>
    /// <remarks>
    /// <para>
    /// The reports are returned in no particular order; however, the data within each report is in ascending order by date (newest first). The report contains rows for only those days that the extension was used.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's analytics reports.</description>
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
    /// <description>The specified extension does not exist.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ExtensionAnalytics>?> GetExtensionAnalytics(TwitchSession session, string? extensionId = null, ExtensionReportType type = ExtensionReportType.OverviewV2, DateTime? startedAt = null, DateTime? endedAt = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.AnalyticsReadExtensions);

        first = Math.Clamp(first, 1, 100);

        NameValueCollection queryParams = new()
        {
            { "first", first.ToString(CultureInfo.InvariantCulture) },
            { "type", type.JsonValue() ?? "" }
        };

        if (!string.IsNullOrWhiteSpace(extensionId))
        {
            queryParams.Add("extension_id", extensionId);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", after);
        }

        if ((!startedAt.HasValue && endedAt.HasValue) || (startedAt.HasValue && !endedAt.HasValue))
        {
            throw new InvalidOperationException("If " + nameof(startedAt) + " is specified, " + nameof(endedAt) + " must also be specified (and vice-versa)").Log(TwitchApi.GetLogger());
        }

        if (startedAt.HasValue && endedAt.HasValue)
        {
            queryParams.Add("started_at", startedAt.Value.Date.ToRfc3339());
            queryParams.Add("ended_at", endedAt.Value.Date.ToRfc3339());
        }

        Uri uri = Util.BuildUri(new("/analytics/extensions"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ExtensionAnalytics>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
