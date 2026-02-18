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

namespace StreamActions.Twitch.Api.Conduits;

/// <summary>
/// Represents an error occurred during a conduit shard update.
/// </summary>
public sealed record ConduitShardUpdateError
{
    /// <summary>
    /// The ID of the shard that failed to update.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// The error code.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}
