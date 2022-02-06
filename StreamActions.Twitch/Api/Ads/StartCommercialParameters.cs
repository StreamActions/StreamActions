/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Ads
{
    /// <summary>
    /// The parameters for <see cref="Commercial.StartCommercial(Common.TwitchSession, StartCommercialParameters)"/>.
    /// </summary>
    public record StartCommercialParameters
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="broadcasterId">ID of the channel requesting a commercial.</param>
        /// <param name="length">Desired length of the commercial in seconds. Valid options are 30, 60, 90, 120, 150, 180.</param>
        /// <exception cref="ArgumentNullException"><paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not one of the valid values.</exception>
        public StartCommercialParameters(string broadcasterId, int length)
        {
            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            if (length is not 30 and not 60 and not 90 and not 120 and not 150 and not 180)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Valid options are 30, 60, 90, 120, 150, 180");
            }

            this.BroadcasterId = broadcasterId;
            this.Length = length;
        }

        /// <summary>
        /// ID of the channel requesting a commercial.
        /// </summary>
        [JsonPropertyName("broadcaster_id")]
        public string BroadcasterId { get; private init; }

        /// <summary>
        /// Desired length of the commercial in seconds. Valid options are 30, 60, 90, 120, 150, 180.
        /// </summary>
        [JsonPropertyName("length")]
        public int Length { get; private init; }
    }
}
