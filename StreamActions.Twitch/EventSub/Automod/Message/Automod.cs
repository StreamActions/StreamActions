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

namespace StreamActions.Twitch.EventSub.Automod.Message;

/// <summary>
/// Information about the automod caught message.
/// </summary>
public sealed record Automod
{
    /// <summary>
    /// The category of the caught message.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>
    /// The level of severity (1-4).
    /// </summary>
    [JsonPropertyName("level")]
    public int? Level { get; init; }

    /// <summary>
    /// The bounds of the text that caused the message to be caught.
    /// </summary>
    [JsonPropertyName("boundaries")]
    public IEnumerable<Boundary>? Boundaries { get; init; }
}
