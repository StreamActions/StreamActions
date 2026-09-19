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
/// The bounds of the text that caused the message to be caught.
/// </summary>
public sealed record Boundary
{
    /// <summary>
    /// Index in the message for the start of the problem (0 indexed, inclusive).
    /// </summary>
    [JsonPropertyName("start_pos")]
    public int? StartPos { get; init; }

    /// <summary>
    /// Index in the message for the end of the problem (0 indexed, inclusive).
    /// </summary>
    [JsonPropertyName("end_pos")]
    public int? EndPos { get; init; }
}
