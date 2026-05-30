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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Extensions;

/// <summary>
/// Contains extension secret data.
/// </summary>
public sealed record ExtensionSecretData
{
    /// <summary>
    /// The raw secret that you use with JWT encoding.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// The UTC date and time that you may begin using this secret to sign a JWT.
    /// </summary>
    [JsonPropertyName("active_at")]
    public DateTime? ActiveAt { get; init; }

    /// <summary>
    /// The UTC date and time that you must stop using this secret to decode a JWT.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }
}
