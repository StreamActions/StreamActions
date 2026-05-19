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
/// Represents the data for banning or timing out a user.
/// </summary>
public sealed record BanUserData
{
    /// <summary>
    /// The ID of the user to ban or put in a timeout.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// To ban a user indefinitely, don't include this field.
    /// To put a user in a timeout, include this field and specify the timeout period, in seconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum timeout is 1 second and the maximum is 1,209,600 seconds (2 weeks).
    /// </para>
    /// <para>
    /// To end a user's timeout early, set this field to 1, or use the Unban user endpoint.
    /// </para>
    /// </remarks>
    [JsonPropertyName("duration")]
    public long? DurationSeconds { get; init; }

    /// <summary>
    /// To ban a user indefinitely, don't include this field.
    /// To put a user in a timeout, include this field and specify the timeout period.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is a helper that returns a <see cref="TimeSpan"/> representation of <see cref="DurationSeconds"/>.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationSeconds.HasValue ? TimeSpan.FromSeconds(this.DurationSeconds.Value) : null;

    /// <summary>
    /// The reason you're banning the user or putting them in a timeout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text is user defined and is limited to a maximum of 500 characters.
    /// </para>
    /// </remarks>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
