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

namespace StreamActions.Twitch.Api.Schedule;

/// <summary>
/// Represents a broadcaster's streaming schedule.
/// </summary>
public sealed record ChannelSchedule
{
    /// <summary>
    /// The list of broadcasts in the broadcaster's streaming schedule.
    /// </summary>
    [JsonPropertyName("segments")]
    public IReadOnlyList<ScheduleSegment>? Segments { get; init; }

    /// <summary>
    /// The ID of the broadcaster that owns the broadcast schedule.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The dates when the broadcaster is on vacation and not streaming.
    /// </summary>
    [JsonPropertyName("vacation")]
    public ScheduleVacation? Vacation { get; init; }

    /// <summary>
    /// Gets the broadcaster's streaming schedule.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the streaming schedule you want to get.</param>
    /// <param name="segmentIds">A list of scheduled segment IDs. Maximum of 100 IDs.</param>
    /// <param name="startTime">The UTC date and time that identifies when in the broadcaster's schedule to start returning segments.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 25. Default: 20.</param>
    /// <param name="after">The cursor that identifies the page of results to retrieve.</param>
    /// <returns>A <see cref="ChannelScheduleResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="session"/> is <see langword="null"/>.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <para>
    /// Too many elements specified in <paramref name="segmentIds"/>.
    /// </para>
    /// </exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's streaming schedule.</description>
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
    /// <description>Only partners and affiliates may add non-recurring broadcast segments.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The broadcaster has not created a streaming schedule.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ChannelScheduleResponse?> GetChannelStreamSchedule(TwitchSession session, string broadcasterId, IEnumerable<string>? segmentIds = null, DateTime? startTime = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        List<string> segmentIdList = segmentIds?.ToList() ?? [];
        if (segmentIdList.Count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentIds), segmentIdList.Count, "The number of segment IDs must not exceed 100.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);

        if (segmentIdList.Count > 0)
        {
            queryParameters.Add("id", segmentIdList);
        }

        if (startTime.HasValue)
        {
            queryParameters.Add("start_time", startTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        }

        first = Math.Clamp(first, 1, 25);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/schedule"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ChannelScheduleResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the broadcaster's streaming schedule as an iCalendar.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the streaming schedule you want to get.</param>
    /// <returns>A <see cref="string"/> containing the iCalendar data, or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="session"/> is <see langword="null"/>.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// </exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's schedule as an iCalendar.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<string?> GetChannelICalendar(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/schedule/icalendar"), queryParameters), session).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Updates the broadcaster's schedule settings, such as scheduling a vacation.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose schedule settings you want to update. This ID must match the user ID in the access token.</param>
    /// <param name="isVacationEnabled">A Boolean value that indicates whether the broadcaster has scheduled a vacation.</param>
    /// <param name="vacationStartTime">The UTC date and time of when the broadcaster's vacation starts.</param>
    /// <param name="vacationEndTime">The UTC date and time of when the broadcaster's vacation ends.</param>
    /// <param name="timezone">The time zone that the broadcaster broadcasts from.</param>
    /// <returns>An <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="session"/> is <see langword="null"/>.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// </exception>
    /// <exception cref="TokenTypeException">
    /// <para>
    /// A user access token is required.
    /// </para>
    /// </exception>
    /// <exception cref="TwitchScopeMissingException">
    /// <para>
    /// The <see cref="Scope.ChannelManageSchedule"/> scope is required.
    /// </para>
    /// </exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully updated the broadcaster's schedule settings.</description>
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
    /// <description>The broadcaster's schedule was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task UpdateChannelStreamSchedule(TwitchSession session, string broadcasterId, bool? isVacationEnabled = null, DateTime? vacationStartTime = null, DateTime? vacationEndTime = null, string? timezone = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageSchedule);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);

        if (isVacationEnabled.HasValue)
        {
            queryParameters.Add("is_vacation_enabled", isVacationEnabled.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
        }

        if (vacationStartTime.HasValue)
        {
            queryParameters.Add("vacation_start_time", vacationStartTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        }

        if (vacationEndTime.HasValue)
        {
            queryParameters.Add("vacation_end_time", vacationEndTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(timezone))
        {
            queryParameters.Add("timezone", timezone);
        }

        _ = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, Util.BuildUri(new("/schedule/settings"), queryParameters), session).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a single or recurring broadcast to the broadcaster's streaming schedule.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the schedule to add the broadcast segment to. This ID must match the user ID in the user access token.</param>
    /// <param name="parameters">The parameters for the new segment.</param>
    /// <returns>A <see cref="ChannelScheduleResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="session"/> is <see langword="null"/>.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="parameters"/> is <see langword="null"/>.
    /// </para>
    /// </exception>
    /// <exception cref="TokenTypeException">
    /// <para>
    /// A user access token is required.
    /// </para>
    /// </exception>
    /// <exception cref="TwitchScopeMissingException">
    /// <para>
    /// The <see cref="Scope.ChannelManageSchedule"/> scope is required.
    /// </para>
    /// </exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully added the broadcast segment.</description>
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
    /// <description>Only partners and affiliates may add non-recurring broadcast segments.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ChannelScheduleResponse?> CreateChannelStreamScheduleSegment(TwitchSession session, string broadcasterId, CreateChannelStreamScheduleSegmentParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageSchedule);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/schedule/segment"), queryParameters), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ChannelScheduleResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a scheduled broadcast segment.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster who owns the broadcast segment to update. This ID must match the user ID in the user access token.</param>
    /// <param name="segmentId">The ID of the broadcast segment to update.</param>
    /// <param name="parameters">The parameters to update.</param>
    /// <returns>A <see cref="ChannelScheduleResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="session"/> is <see langword="null"/>.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="segmentId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="parameters"/> is <see langword="null"/>.
    /// </para>
    /// </exception>
    /// <exception cref="TokenTypeException">
    /// <para>
    /// A user access token is required.
    /// </para>
    /// </exception>
    /// <exception cref="TwitchScopeMissingException">
    /// <para>
    /// The <see cref="Scope.ChannelManageSchedule"/> scope is required.
    /// </para>
    /// </exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the broadcast segment.</description>
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
    /// <description>The specified broadcast segment was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ChannelScheduleResponse?> UpdateChannelStreamScheduleSegment(TwitchSession session, string broadcasterId, string segmentId, UpdateChannelStreamScheduleSegmentParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(segmentId))
        {
            throw new ArgumentNullException(nameof(segmentId)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageSchedule);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("id", segmentId);

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, Util.BuildUri(new("/schedule/segment"), queryParameters), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ChannelScheduleResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a broadcast segment from the broadcaster's streaming schedule.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the streaming schedule. This ID must match the user ID in the user access token.</param>
    /// <param name="segmentId">The ID of the broadcast segment to remove.</param>
    /// <returns>An <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="session"/> is <see langword="null"/>.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="segmentId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// </exception>
    /// <exception cref="TokenTypeException">
    /// <para>
    /// A user access token is required.
    /// </para>
    /// </exception>
    /// <exception cref="TwitchScopeMissingException">
    /// <para>
    /// The <see cref="Scope.ChannelManageSchedule"/> scope is required.
    /// </para>
    /// </exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully removed the broadcast segment.</description>
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
    public static async Task DeleteChannelStreamScheduleSegment(TwitchSession session, string broadcasterId, string segmentId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(segmentId))
        {
            throw new ArgumentNullException(nameof(segmentId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageSchedule);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("id", segmentId);

        _ = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, Util.BuildUri(new("/schedule/segment"), queryParameters), session).ConfigureAwait(false);
    }
}
