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

using StreamActions.Common.Attributes;
using StreamActions.Twitch.Api.Common;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.OAuth;

/// <summary>
/// Record containing response data for the Validate endpoint.
/// </summary>
[ETag("https://dev.twitch.tv/docs/authentication/validate-tokens", "da51777a3ed18ad5014efa29d4bbc78e", "2022-10-16T20:43Z", new string[] {
    "-stripblank", "-strip", "-findfirst", "'<div class=\"main\">'", "-findlast", "'<div class=\"subscribe-footer\">'", "-remre",
    "'cloudcannon[^\"]*'" })]
public record Validate : TwitchResponse
{
    /// <summary>
    /// The returned Client Id attached to the validated OAuth token.
    /// </summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    /// <summary>
    /// The approximate DateTime that <see cref="ExpiresIn"/> refers to.
    /// </summary>
    [JsonIgnore]
    public DateTime Expires => DateTime.UtcNow.AddSeconds(this.ExpiresIn);

    /// <summary>
    /// The number of seconds, in server time, until the OAuth token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    /// <summary>
    /// The login name associated with the OAuth token.
    /// </summary>
    [JsonPropertyName("login")]
    public string? Login { get; init; }

    /// <summary>
    /// The scopes that are authorized under the OAuth token.
    /// </summary>
    [JsonPropertyName("scopes")]
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>
    /// The User Id associated with the OAuth token.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// Validates the OAuth token assigned to the specified <see cref="TwitchSession"/>.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to validate.</param>
    /// <param name="baseAddress">The uri to the Validate endpoint.</param>
    /// <returns>A <see cref="Validate"/> with the current token data or a Twitch error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static async Task<Validate?> ValidateOAuth(TwitchSession session, string baseAddress = "https://id.twitch.tv/oauth2/validate")
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new(baseAddress), session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<Validate>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
