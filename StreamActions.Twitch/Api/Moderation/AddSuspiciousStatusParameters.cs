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
/// Parameters for <see cref="SuspiciousUser.AddSuspiciousStatus(StreamActions.Twitch.Api.Common.TwitchSession, string, string, StreamActions.Twitch.Api.Moderation.AddSuspiciousStatusParameters)"/>.
/// </summary>
public sealed record AddSuspiciousStatusParameters
{
    /// <summary>
    /// The ID of the user being given the suspicious status.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The type of suspicious status.
    /// </summary>
    [JsonPropertyName("status")]
    public SuspiciousUser.SuspiciousStatuses? Status { get; init; }
}
