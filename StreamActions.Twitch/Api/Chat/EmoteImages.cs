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

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// The image URLs for the emote.
/// </summary>
/// <remarks>
/// <para>
/// These image URLs always provide a static, non-animated emote image with a light background.
/// </para>
/// <para>
/// You should use the templated URL in the <see cref="Emote.Template"/> field to fetch the image instead of using these URLs.
/// </para>
/// </remarks>
public sealed record EmoteImages
{
    /// <summary>
    /// A URL to the small version (28px x 28px) of the emote.
    /// </summary>
    [JsonPropertyName("url_1x")]
    public Uri? Url1x { get; init; }

    /// <summary>
    /// A URL to the medium version (56px x 56px) of the emote.
    /// </summary>
    [JsonPropertyName("url_2x")]
    public Uri? Url2x { get; init; }

    /// <summary>
    /// A URL to the large version (112px x 112px) of the emote.
    /// </summary>
    [JsonPropertyName("url_4x")]
    public Uri? Url4x { get; init; }
}
