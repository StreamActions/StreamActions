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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents a suspicious user status in a broadcaster's channel.
/// </summary>
public sealed record SuspiciousUser
{
    /// <summary>
    /// The ID of the user being given the suspicious status.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user ID of the broadcaster indicating in which channel the status is being applied.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The user ID of the moderator who applied the last status.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// The timestamp of the last time this user's status was updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// The type of suspicious status.
    /// </summary>
    [JsonPropertyName("status")]
    public SuspiciousStatuses? Status { get; init; }

    /// <summary>
    /// An array of strings representing the type(s) of suspicious user this is.
    /// </summary>
    [JsonPropertyName("types")]
    public IReadOnlyList<SuspiciousUserTypes>? Types { get; init; }

    /// <summary>
    /// The type of suspicious status.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<SuspiciousStatuses>))]
    public enum SuspiciousStatuses
    {
        /// <summary>
        /// Active monitoring.
        /// </summary>
        [JsonCustomEnum("ACTIVE_MONITORING")]
        ActiveMonitoring,
        /// <summary>
        /// Restricted.
        /// </summary>
        [JsonCustomEnum("RESTRICTED")]
        Restricted,
        /// <summary>
        /// No treatment.
        /// </summary>
        [JsonCustomEnum("NO_TREATMENT")]
        NoTreatment
    }

    /// <summary>
    /// The type of suspicious user.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<SuspiciousUserTypes>))]
    public enum SuspiciousUserTypes
    {
        /// <summary>
        /// Manually added.
        /// </summary>
        [JsonCustomEnum("MANUALLY_ADDED")]
        ManuallyAdded,
        /// <summary>
        /// Detected ban evader.
        /// </summary>
        [JsonCustomEnum("DETECTED_BAN_EVADER")]
        DetectedBanEvader,
        /// <summary>
        /// Detected suspicious chatter.
        /// </summary>
        [JsonCustomEnum("DETECTED_SUS_CHATTER")]
        DetectedSusChatter,
        /// <summary>
        /// Banned in shared channel.
        /// </summary>
        [JsonCustomEnum("BANNED_IN_SHARED_CHANNEL")]
        BannedInSharedChannel
    }

    /// <summary>
    /// Adds a suspicious user status to a chatter on the broadcaster's channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The user ID of the broadcaster, indicating the channel where the status is being applied.</param>
    /// <param name="moderatorId">The user ID of the moderator who is applying the status.</param>
    /// <param name="parameters">The <see cref="AddSuspiciousStatusParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="SuspiciousUser"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/>, <paramref name="parameters"/> is <see langword="null"/>; <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <see cref="AddSuspiciousStatusParameters.UserId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="AddSuspiciousStatusParameters.Status"/> is <see cref="SuspiciousStatuses.NoTreatment"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageSuspiciousUsers"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully applied a suspicious user status.</description>
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
    /// <description>The user in <paramref name="moderatorId"/> is not one of the broadcaster's moderators.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<SuspiciousUser>?> AddSuspiciousStatus(TwitchSession session, string broadcasterId, string moderatorId, AddSuspiciousStatusParameters parameters)
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

        if (string.IsNullOrWhiteSpace(parameters.UserId))
        {
            throw new ArgumentNullException(nameof(parameters.UserId)).Log(TwitchApi.GetLogger());
        }

        if (!parameters.Status.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters.Status)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Status == SuspiciousStatuses.NoTreatment)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Status), "must be ACTIVE_MONITORING or RESTRICTED").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageSuspiciousUsers);

        Uri uri = Util.BuildUri(new("/moderation/suspicious_users"), new() { { "broadcaster_id", broadcasterId }, { "moderator_id", moderatorId } });
        using JsonContent jsonContent = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<SuspiciousUser>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Remove a suspicious user status from a chatter on broadcaster's channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The user ID of the broadcaster, indicating the channel where the status is being removed.</param>
    /// <param name="moderatorId">The user ID of the moderator who is removing the status.</param>
    /// <param name="userId">The ID of the user having the suspicious status removed.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="SuspiciousUser"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageSuspiciousUsers"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully removed a suspicious user status.</description>
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
    /// <description>The user in <paramref name="moderatorId"/> is not one of the broadcaster's moderators.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<SuspiciousUser>?> RemoveSuspiciousStatus(TwitchSession session, string broadcasterId, string moderatorId, string userId)
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

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageSuspiciousUsers);

        Uri uri = Util.BuildUri(new("/moderation/suspicious_users"), new() { { "broadcaster_id", broadcasterId }, { "moderator_id", moderatorId }, { "user_id", userId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<SuspiciousUser>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
