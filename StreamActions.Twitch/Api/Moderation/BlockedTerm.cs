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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents a blocked term in a broadcaster's channel.
/// </summary>
public sealed record BlockedTerm
{
    /// <summary>
    /// The broadcaster that owns the list of blocked terms.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The moderator that blocked the word or phrase from being used in the broadcaster's chat room.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// An ID that identifies this blocked term.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The blocked word or phrase.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) that the term was blocked.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) that the term was updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) that the blocked term is set to expire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field is null if the term was added manually or was permanently blocked by AutoMod.
    /// </para>
    /// </remarks>
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the broadcaster's list of non-private, blocked words or phrases.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose blocked terms you're getting.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="BlockedTerm"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorReadBlockedTerms"/> or <see cref="Scope.ModeratorManageBlockedTerms"/>.</exception>
    /// <remarks>
    /// <para>
    /// The ID in <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorReadBlockedTerms"/> or <see cref="Scope.ModeratorManageBlockedTerms"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of blocked terms.</description>
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
    public static async Task<ResponseData<BlockedTerm>?> GetBlockedTerms(TwitchSession session, string broadcasterId, string moderatorId, int first = 20, string? after = null)
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

        session.RequireUserOrAppToken(Scope.ModeratorReadBlockedTerms, Scope.ModeratorManageBlockedTerms);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);

        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/moderation/blocked_terms"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<BlockedTerm>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a word or phrase to the broadcaster's list of blocked terms.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the list of blocked terms.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="parameters">The <see cref="AddBlockedTermParameters"/> containing the term to block.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="BlockedTerm"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentException"><see cref="AddBlockedTermParameters.Text"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="AddBlockedTermParameters.Text"/> length is out of range.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageBlockedTerms"/>.</exception>
    /// <remarks>
    /// <para>
    /// The ID in <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageBlockedTerms"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully added the blocked term.</description>
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
    public static async Task<ResponseData<BlockedTerm>?> AddBlockedTerm(TwitchSession session, string broadcasterId, string moderatorId, AddBlockedTermParameters parameters)
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

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Text))
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Text.Length < 2 || parameters.Text.Length > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), parameters.Text.Length, "text must be between 2 and 500 characters.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageBlockedTerms);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);

        using JsonContent jsonContent = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/moderation/blocked_terms"), queryParameters), session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<BlockedTerm>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the word or phrase from the broadcaster's list of blocked terms.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the list of blocked terms.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="id">The ID of the blocked term to remove from the broadcaster's list of blocked terms.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <paramref name="id"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageBlockedTerms"/>.</exception>
    /// <remarks>
    /// <para>
    /// The ID in <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageBlockedTerms"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully removed the blocked term.</description>
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
    public static async Task<JsonApiResponse?> RemoveBlockedTerm(TwitchSession session, string broadcasterId, string moderatorId, string id)
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

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageBlockedTerms);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);
        queryParameters.Add("id", id);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, Util.BuildUri(new("/moderation/blocked_terms"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
