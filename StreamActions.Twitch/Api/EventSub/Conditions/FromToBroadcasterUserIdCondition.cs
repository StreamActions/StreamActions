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

namespace StreamActions.Twitch.Api.EventSub.Conditions;

/// <summary>
/// An <see cref="EventSubCondition"/> containing the <c>from_broadcaster_user_id</c> and <c>to_broadcaster_user_id</c> fields.
/// </summary>
public sealed record FromToBroadcasterUserIdCondition : EventSubCondition
{
    /// <summary>
    /// The ID of the sending broadcaster to receive notifications for.
    /// </summary>
    [JsonPropertyName("from_broadcaster_user_id")]
    public string? FromBroadcasterUserId { get; init; }

    /// <summary>
    /// The ID of the receiving broadcaster to receive notifications for.
    /// </summary>
    [JsonPropertyName("to_broadcaster_user_id")]
    public string? ToBroadcasterUserId { get; init; }
}
