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
/// Parameters for <see cref="ExtensionPubSubMessage.SendExtensionPubSubMessage(Common.TwitchSession, SendExtensionPubSubMessageParameters)"/>.
/// </summary>
public sealed record SendExtensionPubSubMessageParameters
{
    /// <summary>
    /// The target of the message. Possible values are: broadcast, global, whisper-&lt;user-id&gt;.
    /// </summary>
    [JsonPropertyName("target")]
    public IReadOnlyList<string>? Target { get; init; }

    /// <summary>
    /// The ID of the broadcaster to send the message to. Don’t include this field if is_global_broadcast is set to true.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the message should be sent to all channels where your extension is active.
    /// </summary>
    [JsonPropertyName("is_global_broadcast")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsGlobalBroadcast { get; init; }

    /// <summary>
    /// The message to send. The message can be a plain-text string or a string-encoded JSON object.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
