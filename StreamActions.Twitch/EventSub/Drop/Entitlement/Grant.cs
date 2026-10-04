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

namespace StreamActions.Twitch.EventSub.Drop.Entitlement;

/// <summary>
/// An event that is sent when a drop entitlement is granted.
/// </summary>
public sealed record Grant : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(DropEntitlementGrantCondition);

    /// <inheritdoc/>
    public static string Type => "drop.entitlement.grant";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// Individual event ID, as assigned by EventSub.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Entitlement object data.
    /// </summary>
    [JsonPropertyName("data")]
    public Entitlement[]? Data { get; init; }
}
