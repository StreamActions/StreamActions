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
/// Describes all views-related information such as how the extension is displayed on mobile devices.
/// </summary>
public sealed record ExtensionViews
{
    /// <summary>
    /// Describes how the extension is displayed on mobile devices.
    /// </summary>
    [JsonPropertyName("mobile")]
    public ExtensionMobileView? Mobile { get; init; }

    /// <summary>
    /// Describes how the extension is rendered if the extension may be activated as a panel extension.
    /// </summary>
    [JsonPropertyName("panel")]
    public ExtensionPanelView? Panel { get; init; }

    /// <summary>
    /// Describes how the extension is rendered if the extension may be activated as a video-overlay extension.
    /// </summary>
    [JsonPropertyName("video_overlay")]
    public ExtensionVideoOverlayView? VideoOverlay { get; init; }

    /// <summary>
    /// Describes how the extension is rendered if the extension may be activated as a video-component extension.
    /// </summary>
    [JsonPropertyName("component")]
    public ExtensionComponentView? Component { get; init; }

    /// <summary>
    /// Describes the view that is shown to broadcasters while they are configuring your extension within the Extension Manager.
    /// </summary>
    [JsonPropertyName("config")]
    public ExtensionConfigView? Config { get; init; }
}
