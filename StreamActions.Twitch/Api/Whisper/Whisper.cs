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
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Whisper;

/// <summary>
/// Sends requests to transmit private messages (Whispers) to other Twitch users.
/// </summary>
public sealed record Whisper
{
    /// <summary>
    /// The whisper message to send. The message must not be empty.
    /// </summary>
    /// <remarks>
    /// The maximum message lengths are:
    /// <list type="bullet">
    /// <item>500 characters if the user you're sending the message to hasn't whispered you before.</item>
    /// <item>10,000 characters if the user you're sending the message to has whispered you before.</item>
    /// </list>
    /// Messages that exceed the maximum length are truncated.
    /// </remarks>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Sends a whisper message to the specified user.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="fromUserId">The ID of the user sending the whisper. This ID must match the user ID in the user access token.</param>
    /// <param name="toUserId">The ID of the user to receive the whisper.</param>
    /// <param name="message">A <see cref="Whisper"/> containing the message to send.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="message"/> is <see langword="null"/>; <paramref name="fromUserId"/>, <paramref name="toUserId"/>, or <see cref="Message"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserManageWhispers"/>.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="fromUserId"/> must have a verified phone number (see the <strong>Phone Number</strong> setting in your Security and Privacy settings).
    /// </para>
    /// <para>
    /// The API may silently drop whispers that it suspects of violating Twitch policies.
    /// The API does not indicate that it dropped the whisper; it returns <strong>204 No Content</strong> as if it succeeded.
    /// </para>
    /// <para>
    /// <strong>Rate Limits</strong>: You may whisper to a maximum of 40 unique recipients per day.
    /// Within the per day limit, you may whisper a maximum of 3 whispers per second and a maximum of 100 whispers per minute.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully sent the whisper message or the message was silently dropped.</description>
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
    /// <description><paramref name="fromUserId"/> may not send whispers due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description><paramref name="toUserId"/> was not found.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description>The whisper rate limit for <paramref name="fromUserId"/> has been exceeded.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> SendWhisper(TwitchSession session, string fromUserId, string toUserId, Whisper message)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.UserManageWhispers);

        if (string.IsNullOrWhiteSpace(fromUserId))
        {
            throw new ArgumentNullException(nameof(fromUserId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(toUserId))
        {
            throw new ArgumentNullException(nameof(toUserId)).Log(TwitchApi.GetLogger());
        }

        if (message is null || string.IsNullOrWhiteSpace(message.Message))
        {
            throw new ArgumentNullException(nameof(message)).Log(TwitchApi.GetLogger());
        }

        Uri uri = Util.BuildUri(new("/whispers"), new() { { "from_user_id", fromUserId }, { "to_user_id", toUserId } });
        using JsonContent jsonContent = JsonContent.Create(message, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
