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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Ads;

/// <summary>
/// The parameters for <see cref="Commercial.StartCommercial(Common.TwitchSession, StartCommercialParameters)"/>.
/// </summary>
public sealed record StartCommercialParameters
{
    /// <summary>
    /// The ID of the partner or affiliate broadcaster that wants to run the commercial. This ID must match the user ID found in the OAuth token.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The length of the commercial to run, in seconds. Twitch tries to serve a commercial that's the requested length, but it may be shorter or longer. The maximum length you should request is 180 seconds.
    /// </summary>
    [JsonPropertyName("length")]
    public int? Length { get; init; }
}
