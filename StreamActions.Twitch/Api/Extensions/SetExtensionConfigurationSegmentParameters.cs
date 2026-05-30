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
/// Parameters for <see cref="ExtensionConfigurationSegment.SetExtensionConfigurationSegment(Common.TwitchSession, SetExtensionConfigurationSegmentParameters)"/>.
/// </summary>
public sealed record SetExtensionConfigurationSegmentParameters
{
    /// <summary>
    /// The ID of the extension to update.
    /// </summary>
    [JsonPropertyName("extension_id")]
    public string? ExtensionId { get; init; }

    /// <summary>
    /// The configuration segment to update. Possible case-sensitive values are: broadcaster, developer, global.
    /// </summary>
    [JsonPropertyName("segment")]
    public string? Segment { get; init; }

    /// <summary>
    /// The ID of the broadcaster that installed the extension. Include this field only if the segment is set to developer or broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The contents of the segment. This string may be a plain-text string or a string-encoded JSON object.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// The version number that identifies this definition of the segment’s data. If not specified, the latest definition is updated.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }
}
