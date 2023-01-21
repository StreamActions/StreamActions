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

using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using StreamActions.Common.Extensions;

namespace StreamActions.Twitch.Api.Ads;

/// <summary>
/// Sends and represents a response element for a request to start a commercial.
/// </summary>
public sealed record Commercial
{
    /// <summary>
    /// The length of the commercial you requested.
    /// </summary>
    [JsonPropertyName("length")]
    public int? Length { get; init; }

    /// <summary>
    /// The number of seconds you must wait before running another commercial.
    /// </summary>
    [JsonPropertyName("retry_after")]
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// The amount of time you must wait before running another commercial.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? RetryAfter => this.RetryAfterSeconds.HasValue ? TimeSpan.FromSeconds(this.RetryAfterSeconds.Value) : null;

    /// <summary>
    /// The timestamp you must wait until before running another commercial.
    /// </summary>
    [JsonIgnore]
    public DateTime? RetryAfterDateTime => this.RetryAfter.HasValue ? DateTime.UtcNow.Add(this.RetryAfter.Value) : null;

    /// <summary>
    /// A message that indicates whether Twitch was able to serve an ad.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Starts a commercial on the specified channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="StartCommercialParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Commercial"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><see cref="StartCommercialParameters.BroadcasterId"/> is <see langword="null"/>, empty, or whitespace; <see cref="StartCommercialParameters.Length"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelEditCommercial"/>.</exception>
    /// <remarks>
    /// <para>
    /// Only partners and affiliates may run commercials and they must be streaming live at the time.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully started the commercial.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The specified broadcaster does not exist.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description>The cooldown has not expired yet.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<Commercial>?> StartCommercial(TwitchSession session, StartCommercialParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.BroadcasterId))
        {
            throw new ArgumentNullException(nameof(parameters.BroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (!parameters.Length.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters.Length)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelEditCommercial);

        if (Math.Clamp(parameters.Length.Value, 1, 180) != parameters.Length.Value)
        {
            parameters = parameters with { Length = Math.Clamp(parameters.Length.Value, 1, 180) };
        }

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("/channels/commercial"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Commercial>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
