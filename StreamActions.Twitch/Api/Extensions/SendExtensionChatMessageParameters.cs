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

namespace StreamActions.Twitch.Api.Extensions;

/// <summary>
/// Parameters for <see cref="ExtensionChatMessage.SendExtensionChatMessage(Common.TwitchSession, string, SendExtensionChatMessageParameters)"/>.
/// </summary>
public sealed record SendExtensionChatMessageParameters
{
    /// <summary>
    /// The message. The message may contain a maximum of 280 characters.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// The ID of the extension that’s sending the chat message.
    /// </summary>
    [JsonPropertyName("extension_id")]
    public string? ExtensionId { get; init; }

    /// <summary>
    /// The extension’s version number.
    /// </summary>
    [JsonPropertyName("extension_version")]
    public string? ExtensionVersion { get; init; }
}
