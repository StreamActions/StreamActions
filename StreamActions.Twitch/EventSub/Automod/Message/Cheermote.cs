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
/// Metadata pertaining to the cheermote.
/// </summary>
public sealed record Cheermote
{
    /// <summary>
    /// The name portion of the Cheermote string that you use in chat to cheer Bits, converted to lowercase.
    /// </summary>
    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    /// <summary>
    /// The amount of Bits cheered.
    /// </summary>
    [JsonPropertyName("bits")]
    public int? Bits { get; init; }

    /// <summary>
    /// The tier level of the cheermote.
    /// </summary>
    [JsonPropertyName("tier")]
    public int? Tier { get; init; }
}
