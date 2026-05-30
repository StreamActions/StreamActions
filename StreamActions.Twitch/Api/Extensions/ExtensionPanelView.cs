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
/// Describes how the extension is rendered if the extension may be activated as a panel extension.
/// </summary>
public sealed record ExtensionPanelView
{
    /// <summary>
    /// The HTML file that is shown to viewers on the channel page when the extension is activated in a Panel slot.
    /// </summary>
    [JsonPropertyName("viewer_url")]
    public string? ViewerUrl { get; init; }

    /// <summary>
    /// The height, in pixels, of the panel component that the extension is rendered in.
    /// </summary>
    [JsonPropertyName("height")]
    public int? Height { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the extension can link to non-Twitch domains.
    /// </summary>
    [JsonPropertyName("can_link_external_content")]
    public bool? CanLinkExternalContent { get; init; }
}
