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
/// Parameters for <see cref="ExtensionRequiredConfiguration.SetExtensionRequiredConfiguration(Common.TwitchSession, string, SetExtensionRequiredConfigurationParameters)"/>.
/// </summary>
public sealed record SetExtensionRequiredConfigurationParameters
{
    /// <summary>
    /// The ID of the extension to update.
    /// </summary>
    [JsonPropertyName("extension_id")]
    public string? ExtensionId { get; init; }

    /// <summary>
    /// The version of the extension to update.
    /// </summary>
    [JsonPropertyName("extension_version")]
    public string? ExtensionVersion { get; init; }

    /// <summary>
    /// The required_configuration string to use with the extension.
    /// </summary>
    [JsonPropertyName("required_configuration")]
    public string? RequiredConfiguration { get; init; }
}
