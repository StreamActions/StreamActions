/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Ads;

/// <summary>
/// Sends and represents a response element for a request to start a commercial.
/// </summary>
public sealed record Commercial
{
    /// <summary>
    /// Length of the triggered commercial.
    /// </summary>
    [JsonPropertyName("length")]
    public int? Length { get; init; }

    /// <summary>
    /// Seconds until the next commercial can be served on this channel.
    /// </summary>
    [JsonPropertyName("retry_after")]
    public int? RetryAfter { get; init; }

    /// <summary>
    /// The timestamp represented by <see cref="RetryAfter"/>.
    /// </summary>
    [JsonIgnore]
    public DateTime? RetryAfterDate => this.RetryAfter is null ? null : DateTime.UtcNow.AddSeconds((double)this.RetryAfter);

    /// <summary>
    /// Provides contextual information on why the request failed.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Starts a commercial on a specified channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="StartCommercialParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Commercial"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelEditCommercial"/>.</exception>
    public static async Task<ResponseData<Commercial>?> StartCommercial(TwitchSession session, StartCommercialParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        session.RequireToken(Scope.ChannelEditCommercial);

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("/channels/commercial"), session, content).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResponseData<Commercial>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
