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
}
