/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.Ads;

/// <summary>
/// The parameters for <see cref="Commercial.StartCommercial(Common.TwitchSession, StartCommercialParameters)"/>.
/// </summary>
public sealed record StartCommercialParameters
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="broadcasterId">ID of the channel requesting a commercial.</param>
    /// <param name="length">Desired length of the commercial in seconds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="broadcasterId"/> is null, empty, or whitespace; <paramref name="length"/> is <see cref="CommercialLength.None"/></exception>
    public StartCommercialParameters(string broadcasterId, CommercialLength length)
    {
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId));
        }

        if (length == CommercialLength.None)
        {
            throw new ArgumentNullException(nameof(length));
        }

        this.BroadcasterId = broadcasterId;
        this.Length = (int)length;
    }

    /// <summary>
    /// ID of the channel requesting a commercial.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string BroadcasterId { get; private init; }

    /// <summary>
    /// Desired length of the commercial in seconds.
    /// </summary>
    [JsonPropertyName("length")]
    public int Length { get; private init; }

    /// <summary>
    /// Valid lengths for a commercial break.
    /// </summary>
    public enum CommercialLength
    {
        /// <summary>
        /// Default value. Not valid for use.
        /// </summary>
        None,
        /// <summary>
        /// 30 seconds.
        /// </summary>
        Thirty = 30,
        /// <summary>
        /// 1 minute.
        /// </summary>
        Sixty = 60,
        /// <summary>
        /// 1 minute, 30 seconds.
        /// </summary>
        Ninety = 90,
        /// <summary>
        /// 2 minutes.
        /// </summary>
        OneTwenty = 120,
        /// <summary>
        /// 2 minutes, 30 seconds.
        /// </summary>
        OneFifty = 150,
        /// <summary>
        /// 3 minutes.
        /// </summary>
        OneEighty = 180
    }
}
