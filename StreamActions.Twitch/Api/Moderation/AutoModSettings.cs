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
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents the broadcaster's AutoMod settings.
/// </summary>
public sealed record AutoModSettings
{
    /// <summary>
    /// The broadcaster's ID.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The moderator's ID.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// The default AutoMod level for the broadcaster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field is null if the broadcaster has set one or more of the individual settings.
    /// </para>
    /// </remarks>
    [JsonPropertyName("overall_level")]
    public int? OverallLevel { get; init; }

    /// <summary>
    /// The Automod level for discrimination against disability.
    /// </summary>
    [JsonPropertyName("disability")]
    public int? Disability { get; init; }

    /// <summary>
    /// The Automod level for hostility involving aggression.
    /// </summary>
    [JsonPropertyName("aggression")]
    public int? Aggression { get; init; }

    /// <summary>
    /// The AutoMod level for discrimination based on sexuality, sex, or gender.
    /// </summary>
    [JsonPropertyName("sexuality_sex_or_gender")]
    public int? SexualitySexOrGender { get; init; }

    /// <summary>
    /// The Automod level for discrimination against women.
    /// </summary>
    [JsonPropertyName("misogyny")]
    public int? Misogyny { get; init; }

    /// <summary>
    /// The Automod level for hostility involving name calling or insults.
    /// </summary>
    [JsonPropertyName("bullying")]
    public int? Bullying { get; init; }

    /// <summary>
    /// The Automod level for profanity.
    /// </summary>
    [JsonPropertyName("swearing")]
    public int? Swearing { get; init; }

    /// <summary>
    /// The Automod level for racial discrimination.
    /// </summary>
    [JsonPropertyName("race_ethnicity_or_religion")]
    public int? RaceEthnicityOrReligion { get; init; }

    /// <summary>
    /// The Automod level for sexual content.
    /// </summary>
    [JsonPropertyName("sex_based_terms")]
    public int? SexBasedTerms { get; init; }

    /// <summary>
    /// Gets the broadcaster's AutoMod settings.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose AutoMod settings you want to get.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="AutoModSettings"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorReadAutomodSettings"/> or <see cref="Scope.ModeratorManageAutomodSettings"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorReadAutomodSettings"/> or <see cref="Scope.ModeratorManageAutomodSettings"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's AutoMod settings.</description>
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
    public static async Task<ResponseData<AutoModSettings>?> GetAutoModSettings(TwitchSession session, string broadcasterId, string moderatorId)
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

        session.RequireUserOrAppToken(Scope.ModeratorReadAutomodSettings, Scope.ModeratorManageAutomodSettings);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/moderation/automod/settings"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<AutoModSettings>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the broadcaster's AutoMod settings.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose AutoMod settings you want to update.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="settings">The <see cref="AutoModSettings"/> to apply.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="AutoModSettings"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="settings"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageAutomodSettings"/>.</exception>
    /// <remarks>
    /// <para>
    /// Because PUT is an overwrite operation, you must include all the fields that you want set after the operation completes.
    /// </para>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageAutomodSettings"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the broadcaster's AutoMod settings.</description>
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
    public static async Task<ResponseData<AutoModSettings>?> UpdateAutoModSettings(TwitchSession session, string broadcasterId, string moderatorId, AutoModSettings settings)
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

        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageAutomodSettings);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);

        using JsonContent jsonContent = JsonContent.Create(settings, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, Util.BuildUri(new("/moderation/automod/settings"), queryParameters), session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<AutoModSettings>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
