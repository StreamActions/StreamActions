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
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;

namespace StreamActions.Twitch.EventSub.Conduit.Shard;

/// <summary>
/// An event that is sent when a conduit shard is disabled.
/// </summary>
public sealed record Disabled : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(ConduitCondition);

    /// <inheritdoc/>
    public static string Type => "conduit.shard.disabled";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The ID of the conduit.
    /// </summary>
    [JsonPropertyName("conduit_id")]
    public string? ConduitId { get; init; }

    /// <summary>
    /// The ID of the disabled shard.
    /// </summary>
    [JsonPropertyName("shard_id")]
    public string? ShardId { get; init; }

    /// <summary>
    /// The new status of the transport.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>
    /// The disabled transport.
    /// </summary>
    [JsonPropertyName("transport")]
    public Transport? Transport { get; init; }
}
