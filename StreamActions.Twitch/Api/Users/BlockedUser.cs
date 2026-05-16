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
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Users;

/// <summary>
/// Represents a user that the broadcaster has blocked.
/// </summary>
public sealed record BlockedUser
{
    /// <summary>
    /// The reason that the broadcaster is blocking the user.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<BlockReason>))]
    public enum BlockReason
    {
        /// <summary>
        /// Harassment.
        /// </summary>
        [JsonCustomEnum("harassment")]
        Harassment,
        /// <summary>
        /// Spam.
        /// </summary>
        [JsonCustomEnum("spam")]
        Spam,
        /// <summary>
        /// Other.
        /// </summary>
        [JsonCustomEnum("other")]
        Other
    }

    /// <summary>
    /// The location where the harassment took place.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<BlockSourceContext>))]
    public enum BlockSourceContext
    {
        /// <summary>
        /// Chat.
        /// </summary>
        [JsonCustomEnum("chat")]
        Chat,
        /// <summary>
        /// Whisper.
        /// </summary>
        [JsonCustomEnum("whisper")]
        Whisper
    }

    /// <summary>
    /// An ID that identifies the blocked user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The blocked user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The blocked user's display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the list of users that the broadcaster has blocked.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose list of blocked users you want to get.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="BlockedUser"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserReadBlockedUsers"/>.</exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's list of blocked users.</description>
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
    public static async Task<ResponseData<BlockedUser>?> GetBlockedUsers(TwitchSession session, string broadcasterId, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.UserReadBlockedUsers);

        NameValueCollection queryParams = [];
        queryParams.Add("broadcaster_id", broadcasterId);

        first = Math.Clamp(first, 1, 100);
        queryParams.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", after);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/users/blocks"), queryParams), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<BlockedUser>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Blocks the specified user from interacting with or having contact with the broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="targetUserId">The ID of the user to block.</param>
    /// <param name="sourceContext">The location where the harassment took place that is causing the broadcaster to block the user.</param>
    /// <param name="reason">The reason that the broadcaster is blocking the user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="targetUserId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserManageBlockedUsers"/>.</exception>
    /// <remarks>
    /// <para>
    /// The user ID in the access token identifies the broadcaster who is blocking the user.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully blocked the user.</description>
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
    public static async Task BlockUser(TwitchSession session, string targetUserId, BlockSourceContext? sourceContext = null, BlockReason? reason = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            throw new ArgumentNullException(nameof(targetUserId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.UserManageBlockedUsers);

        NameValueCollection queryParams = [];
        queryParams.Add("target_user_id", targetUserId);

        if (sourceContext.HasValue)
        {
            queryParams.Add("source_context", sourceContext.Value.JsonValue());
        }

        if (reason.HasValue)
        {
            queryParams.Add("reason", reason.Value.JsonValue());
        }

        _ = await TwitchApi.PerformHttpRequest(HttpMethod.Put, Util.BuildUri(new("/users/blocks"), queryParams), session).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the user from the broadcaster's list of blocked users.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="targetUserId">The ID of the user to remove from the broadcaster's list of blocked users.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="targetUserId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserManageBlockedUsers"/>.</exception>
    /// <remarks>
    /// <para>
    /// The user ID in the access token identifies the broadcaster who is removing the block.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully removed the block.</description>
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
    public static async Task UnblockUser(TwitchSession session, string targetUserId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            throw new ArgumentNullException(nameof(targetUserId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.UserManageBlockedUsers);

        NameValueCollection queryParams = [];
        queryParams.Add("target_user_id", targetUserId);

        _ = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, Util.BuildUri(new("/users/blocks"), queryParams), session).ConfigureAwait(false);
    }
}
