/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.Polls;

/// <summary>
/// Represents the parameters for <see cref="Poll.EndPoll(Common.TwitchSession, PollEndParameters)"/>
/// </summary>
public sealed record PollEndParameters
{
    /// <summary>
    /// The ID of the broadcaster that’s running the poll. This ID must match the user ID in the user access token.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The ID of the poll to update.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The status to set the poll to.
    /// </summary>
    /// <remarks>
    /// Possible case-sensitive values are:
    /// <list type="table">
    /// <item>
    /// <term>TERMINATED</term>
    /// <description>Ends the poll before the poll is scheduled to end. The poll remains publicly visible.</description>
    /// </item>
    /// <item>
    /// <term>ARCHIVED</term>
    /// <description>Ends the poll before the poll is scheduled to end, and then archives it so it's no longer publicly visible.</description>
    /// </item>
    /// </list>
    /// </remarks>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
