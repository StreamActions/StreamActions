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

namespace StreamActions.Twitch.Api.Bits
{
    /// <summary>
    /// Represents the uris to the images in a <see cref="CheermoteImageSet"/> in various sizes.
    /// </summary>
    public record CheermoteImageUris
    {
        /// <summary>
        /// The uri to the 1x size image.
        /// </summary>
        [JsonPropertyName("1")]
        public Uri? Uri1x { get; init; }

        /// <summary>
        /// The uri to the 1.5x size image.
        /// </summary>
        [JsonPropertyName("1.5")]
        public Uri? Uri1p5x { get; init; }

        /// <summary>
        /// The uri to the 2x size image.
        /// </summary>
        [JsonPropertyName("2")]
        public Uri? Uri2x { get; init; }

        /// <summary>
        /// The uri to the 3x size image.
        /// </summary>
        [JsonPropertyName("3")]
        public Uri? Uri3x { get; init; }

        /// <summary>
        /// The uri to the 4x size image.
        /// </summary>
        [JsonPropertyName("4")]
        public Uri? Uri4x { get; init; }
    }
}
