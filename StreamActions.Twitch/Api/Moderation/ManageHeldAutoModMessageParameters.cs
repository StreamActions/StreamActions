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
/// Represents the parameters for managing a held AutoMod message.
/// </summary>
public sealed record ManageHeldAutoModMessageParameters
{
    /// <summary>
    /// The moderator who is approving or denying the held message.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The ID of the message to allow or deny.
    /// </summary>
    [JsonPropertyName("msg_id")]
    public string? MsgId { get; init; }

    /// <summary>
    /// The action to take for the message.
    /// </summary>
    [JsonPropertyName("action")]
    public AutoModActions? Action { get; init; }

    /// <summary>
    /// The action to take for the message.
    /// </summary>
    [JsonConverter(typeof(JsonUpperCaseEnumConverter<AutoModActions>))]
    public enum AutoModActions
    {
        /// <summary>
        /// Allow the message.
        /// </summary>
        Allow,
        /// <summary>
        /// Deny the message.
        /// </summary>
        Deny
    }
}
