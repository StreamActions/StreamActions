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
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents an unban request for a broadcaster's channel.
/// </summary>
public sealed record UnbanRequest
{
    /// <summary>
    /// Unban request ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// User ID of broadcaster whose channel is receiving the unban request.
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
    /// User ID of moderator who approved/denied the request.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// The moderator's login name.
    /// </summary>
    [JsonPropertyName("moderator_login")]
    public string? ModeratorLogin { get; init; }

    /// <summary>
    /// The moderator's display name.
    /// </summary>
    [JsonPropertyName("moderator_name")]
    public string? ModeratorName { get; init; }

    /// <summary>
    /// User ID of the requestor who is asking for an unban.
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
    /// Text of the request from the requesting user.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Status of the request.
    /// </summary>
    [JsonPropertyName("status")]
    public UnbanRequestStatuses? Status { get; init; }

    /// <summary>
    /// Timestamp of when the unban request was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// Timestamp of when moderator/broadcaster approved or denied the request.
    /// </summary>
    [JsonPropertyName("resolved_at")]
    public DateTime? ResolvedAt { get; init; }

    /// <summary>
    /// Text input by the resolver (moderator) of the unban request.
    /// </summary>
    [JsonPropertyName("resolution_text")]
    public string? ResolutionText { get; init; }

    /// <summary>
    /// Status of the unban request.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<UnbanRequestStatuses>))]
    public enum UnbanRequestStatuses
    {
        /// <summary>
        /// Pending.
        /// </summary>
        [JsonCustomEnum("pending")]
        Pending,
        /// <summary>
        /// Approved.
        /// </summary>
        [JsonCustomEnum("approved")]
        Approved,
        /// <summary>
        /// Denied.
        /// </summary>
        [JsonCustomEnum("denied")]
        Denied,
        /// <summary>
        /// Acknowledged.
        /// </summary>
        [JsonCustomEnum("acknowledged")]
        Acknowledged,
        /// <summary>
        /// Canceled.
        /// </summary>
        [JsonCustomEnum("canceled")]
        Canceled
    }

    /// <summary>
    /// Gets a list of unban requests for a broadcaster's channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose channel is receiving unban requests.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's unban requests.</param>
    /// <param name="status">Filter by a status.</param>
    /// <param name="userId">The ID used to filter what unban requests are returned.</param>
    /// <param name="after">Cursor used to get next page of results.</param>
    /// <param name="first">The maximum number of items to return per page in response.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="UnbanRequest"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorReadUnbanRequests"/> or <see cref="Scope.ModeratorManageUnbanRequests"/>.</exception>
    /// <remarks>
    /// <para>
    /// The ID in <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of unban requests.</description>
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
    public static async Task<ResponseData<UnbanRequest>?> GetUnbanRequests(TwitchSession session, string broadcasterId, string moderatorId, UnbanRequestStatuses status, string? userId = null, string? after = null, int first = 20)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ModeratorReadUnbanRequests, Scope.ModeratorManageUnbanRequests);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);
        queryParameters.Add("status", status.JsonValue() ?? "");

        if (!string.IsNullOrWhiteSpace(userId))
        {
            queryParameters.Add("user_id", userId);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/moderation/unban_requests"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<UnbanRequest>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves an unban request by approving or denying it.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose channel is approving or denying the unban request.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's unban requests.</param>
    /// <param name="unbanRequestId">The ID of the unban request to resolve.</param>
    /// <param name="parameters">The <see cref="ResolveUnbanRequestParameters"/> containing the resolution details.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="UnbanRequest"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/>, <paramref name="parameters"/> is <see langword="null"/>; <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <paramref name="unbanRequestId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ResolveUnbanRequestParameters.ResolutionText"/> is too long.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageUnbanRequests"/>.</exception>
    /// <remarks>
    /// <para>
    /// The ID in <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully resolved the unban request.</description>
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
    /// <description>The unban request ID was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<UnbanRequest>?> ResolveUnbanRequest(TwitchSession session, string broadcasterId, string moderatorId, string unbanRequestId, ResolveUnbanRequestParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(unbanRequestId))
        {
            throw new ArgumentNullException(nameof(unbanRequestId)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (parameters.ResolutionText is not null && parameters.ResolutionText.Length > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), parameters.ResolutionText.Length, "resolution_text must be 500 characters or less.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ModeratorManageUnbanRequests);

        NameValueCollection queryParameters = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "unban_request_id", unbanRequestId }
        };

        Uri uri = Util.BuildUri(new("/moderation/unban_requests"), queryParameters);
        using JsonContent jsonContent = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, uri, session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<UnbanRequest>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
