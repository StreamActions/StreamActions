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
using StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomReward.Objects;

namespace StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomReward.Objects;

/// <summary>
/// Set of images for the reward.
/// </summary>
public sealed record Image
{
    /// <summary>
    /// URL for the image at 1x size.
    /// </summary>
    [JsonPropertyName("url_1x")]
    public Uri? Url1x { get; init; }

    /// <summary>
    /// URL for the image at 2x size.
    /// </summary>
    [JsonPropertyName("url_2x")]
    public Uri? Url2x { get; init; }

    /// <summary>
    /// URL for the image at 4x size.
    /// </summary>
    [JsonPropertyName("url_4x")]
    public Uri? Url4x { get; init; }
}
