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

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents the parameters for adding a blocked term.
/// </summary>
public sealed record AddBlockedTermParameters
{
    /// <summary>
    /// The word or phrase to block from being used in the broadcaster’s chat room.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The term must contain a minimum of 2 characters and may contain up to a maximum of 500 characters.
    /// </para>
    /// <para>
    /// Terms may include a wildcard character (*). The wildcard character must appear at the beginning or end of a word or set of characters. For example, *foo or foo*.
    /// </para>
    /// </remarks>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}
