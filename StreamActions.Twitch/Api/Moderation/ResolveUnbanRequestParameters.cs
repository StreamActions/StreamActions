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

using StreamActions.Common.Json.Serialization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents the parameters for resolving an unban request.
/// </summary>
public sealed record ResolveUnbanRequestParameters
{
    /// <summary>
    /// The resolution status.
    /// </summary>
    [JsonPropertyName("status")]
    public UnbanRequestResolutions? Status { get; init; }

    /// <summary>
    /// Message supplied by the unban request resolver.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The message is limited to a maximum of 500 characters.
    /// </para>
    /// </remarks>
    [JsonPropertyName("resolution_text")]
    public string? ResolutionText { get; init; }

    /// <summary>
    /// The resolution status.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<UnbanRequestResolutions>))]
    public enum UnbanRequestResolutions
    {
        /// <summary>
        /// Approved.
        /// </summary>
        [JsonCustomEnum("approved")]
        Approved,
        /// <summary>
        /// Denied.
        /// </summary>
        [JsonCustomEnum("denied")]
        Denied
    }
}
