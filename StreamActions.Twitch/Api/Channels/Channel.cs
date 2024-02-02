/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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
using System.Net.Http.Json;
using StreamActions.Common.Net;
using StreamActions.Common.Extensions;
using System.Text.RegularExpressions;

namespace StreamActions.Twitch.Api.Channels;

/// <summary>
/// Sends and represents a response element for a request for channel information.
/// </summary>
public sealed partial record Channel
{
    /// <summary>
    /// An ID that uniquely identifies the broadcaster.
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
    public string? BroadasterName { get; init; }

    /// <summary>
    /// The name of the game that the broadcaster is playing or last played. The value is an empty string if the broadcaster has never played a game.
    /// </summary>
    [JsonPropertyName("game_name")]
    public string? GameName { get; init; }

    /// <summary>
    /// An ID that uniquely identifies the game that the broadcaster is playing or last played. The value is an empty string if the broadcaster has never played a game.
    /// </summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; init; }

    /// <summary>
    /// The broadcaster's preferred language.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is an ISO 639-1 two-letter language code (for example, <c>"en"</c> for English). The value is set to <c>"other"</c> if the language is not a Twitch supported language.
    /// </para>
    /// </remarks>
    [JsonPropertyName("broadcaster_language")]
    public string? BroadcasterLanguage { get; init; }

    /// <summary>
    /// The title of the stream that the broadcaster is currently streaming or last streamed. The value is an empty string if the broadcaster has never streamed.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The value of the broadcaster's stream delay setting, in seconds. This field's value defaults to zero unless 1) the request specifies a user access token, 2) the ID in the <c>broadcasterId</c> parameter matches the user ID in the access token, and 3) the broadcaster has partner status and they set a non-zero stream delay value.
    /// </summary>
    [JsonPropertyName("delay")]
    public int? Delay { get; init; }

    /// <summary>
    /// The tags applied to the channel.
    /// </summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// The <see cref="Regex"/> validating tags.
    /// </summary>
    [GeneratedRegex("^[^\\W]+$")]
    private static partial Regex TagsRegex();

    /// <summary>
    /// Gets information about one or more channels.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose channel you want to get. You may specify a maximum of 100 IDs.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Channel"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/> or has 0 elements.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="broadcasterId"/> has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of channels.</description>
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
    public static async Task<ResponseData<Channel>?> GetChannelInformation(TwitchSession session, IEnumerable<string> broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        if (broadcasterId is null || !broadcasterId.Any())
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (broadcasterId.Count() > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(broadcasterId), broadcasterId.Count(), "must have a count <= 100").Log(TwitchApi.GetLogger());
        }

        Uri uri = Util.BuildUri(new("/channels"), new() { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Channel>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Modifies channel information for users.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose channel you want to update. This ID must match the user ID in the user access token.</param>
    /// <param name="parameters">The <see cref="ModifyChannelParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><see cref="ModifyChannelParameters.Title"/> is empty or whitespace; an element in <see cref="ModifyChannelParameters.Tags"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ModifyChannelParameters.Tags"/> has more than 10 elements; an element in <see cref="ModifyChannelParameters.Tags"/> has more than 25 characters, a whitespace character, or a special character.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageBroadcast"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully updated the channel's properties.</description>
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<JsonApiResponse?> ModifyChannelInformation(TwitchSession session, string broadcasterId, ModifyChannelParameters parameters)
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

        session.RequireToken(Scope.ChannelManageBroadcast);

        if (parameters.Title is not null && string.IsNullOrWhiteSpace(parameters.Title))
        {
            throw new ArgumentNullException(nameof(parameters.Title)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Tags is not null && parameters.Tags.Count > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Tags), parameters.Tags.Count, "must have a count <= 10").Log(TwitchApi.GetLogger());
        }

        if (parameters.Tags is not null && parameters.Tags.Any())
        {
            foreach (string tag in parameters.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    throw new ArgumentNullException(nameof(parameters.Tags), "a tag may not be null, empty, or whitespace").Log(TwitchApi.GetLogger());
                }
                else if (!TagsRegex().IsMatch(tag))
                {
                    throw new ArgumentOutOfRangeException(nameof(parameters.Tags), tag, "a tag may not contain whitespace or special characters").Log(TwitchApi.GetLogger());
                }
                else if (tag.Length > 25)
                {
                    throw new ArgumentOutOfRangeException(nameof(parameters.Tags), tag, "a tags length must be <= 25").Log(TwitchApi.GetLogger());
                }
            }
        }

        if (parameters.Delay.HasValue && Math.Clamp(parameters.Delay.Value, 0, 900) != parameters.Delay.Value)
        {
            parameters = parameters with { Delay = Math.Clamp(parameters.Delay.Value, 0, 900) };
        }

        Uri uri = Util.BuildUri(new("/channels"), new() { { "broadcaster_id", broadcasterId } });
        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
