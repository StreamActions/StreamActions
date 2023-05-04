/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Text.Json.Serialization;
using StreamActions.Common.Extensions;
using System.Net.Http.Json;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Sends and represents a response element for a request to retrieve or update the status of Shield Mode.
/// </summary>
public sealed record ShieldMode
{
    /// <summary>
    /// An ID that identifies the moderator that last activated Shield Mode.
    /// </summary>
    /// <remarks>
    /// Is an empty string if Shield Mode hasn't been previously activated.
    /// </remarks>
    [JsonPropertyName("moderator_id")]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// The moderator's login name.
    /// </summary>
    /// <remarks>
    /// Is an empty string if Shield Mode hasn't been previously activated.
    /// </remarks>
    [JsonPropertyName("moderator_login")]
    public string? ModeratorLogin { get; init; }

    /// <summary>
    /// The moderator's display name.
    /// </summary>
    /// <remarks>
    /// Is an empty string if Shield Mode hasn't been previously activated.
    /// </remarks>
    [JsonPropertyName("moderator_name")]
    public string? ModeratorName { get; init; }

    /// <summary>
    /// A Boolean value that determines whether Shield Mode is active.
    /// </summary>
    [JsonPropertyName("is_active")]
    public bool? IsActive { get; init; }

    /// <summary>
    /// The UTC timestamp of when Shield Mode was last activated.
    /// </summary>
    [JsonPropertyName("last_activated_at")]
    public DateTime? LastActivatedAt { get; init; }

    /// <summary>
    /// Gets the broadcaster's Shield Mode activation status.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose Shield Mode activation status you want to get.</param>
    /// <param name="moderatorId">The ID of the broadcaster or one of the broadcaster's moderators. This ID must match the user ID in the user access token.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ShieldMode"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorReadShieldMode"/> or <see cref="Scope.ModeratorManageShieldMode"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's Shield Mode activation status.</description>
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
    /// <description><paramref name="moderatorId"/> is not a moderator for <paramref name="broadcasterId"/>.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ShieldMode>?> GetShieldModeStatus(TwitchSession session, string broadcasterId, string moderatorId)
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

        session.RequireToken(Scope.ModeratorReadShieldMode, Scope.ModeratorManageShieldMode);

        Uri uri = Util.BuildUri(new("/moderation/shield_mode"), new() { { "broadcaster_id", broadcasterId }, { "moderator_id", moderatorId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ShieldMode>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Activates or deactivates the broadcaster's Shield Mode.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose Shield Mode activation status you want to activate or deactivate.</param>
    /// <param name="moderatorId">The ID of the broadcaster or one of the broadcaster's moderators. This ID must match the user ID in the user access token.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ShieldMode"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="parameters"/> or <see cref="ShieldModeUpdateParameters.IsActive"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageShieldMode"/>.</exception>
    /// <remarks>
    /// <para>
    /// To update the restrictions that are used when Shield Mode is active, a moderator must visit the Creator Dashboard for the broadcaster on Twitch.
    /// </para>
    /// <para>
    /// To add an auto-ban phrase to Shield Mode, or manage users who were banned by an auto-ban phrase, a moderator must visit the Creator Dashboard for the broadcaster on Twitch while Shield Mode is active.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the broadcaster's Shield Mode activation status.</description>
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
    /// <description><paramref name="moderatorId"/> is not a moderator for <paramref name="broadcasterId"/>.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ShieldMode>?> UpdateShieldModeStatus(TwitchSession session, string broadcasterId, string moderatorId, ShieldModeUpdateParameters parameters)
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

        if (parameters is null || !parameters.IsActive.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ModeratorManageShieldMode);

        Uri uri = Util.BuildUri(new("/moderation/shield_mode"), new() { { "broadcaster_id", broadcasterId }, { "moderator_id", moderatorId } });
        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ShieldMode>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
