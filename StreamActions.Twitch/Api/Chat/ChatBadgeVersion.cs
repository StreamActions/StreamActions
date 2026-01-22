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

using System;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// A version of a chat badge.
/// </summary>
public sealed record ChatBadgeVersion
{
    /// <summary>
    /// An ID that identifies this version of the badge.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// A URL to the small version (18px x 18px) of the badge.
    /// </summary>
    [JsonPropertyName("image_url_1x")]
    public Uri? ImageUrl1x { get; init; }

    /// <summary>
    /// A URL to the medium version (36px x 36px) of the badge.
    /// </summary>
    [JsonPropertyName("image_url_2x")]
    public Uri? ImageUrl2x { get; init; }

    /// <summary>
    /// A URL to the large version (72px x 72px) of the badge.
    /// </summary>
    [JsonPropertyName("image_url_4x")]
    public Uri? ImageUrl4x { get; init; }

    /// <summary>
    /// The title of the badge.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The description of the badge.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// The action to take when clicking on the badge.
    /// </summary>
    [JsonPropertyName("click_action")]
    public string? ClickAction { get; init; }

    /// <summary>
    /// The URL to navigate to when clicking on the badge.
    /// </summary>
    [JsonPropertyName("click_url")]
    public Uri? ClickUrl { get; init; }
}
