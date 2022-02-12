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

namespace StreamActions.Twitch.Api.Common
{
    /// <summary>
    /// An image url object.
    /// </summary>
    public record Images
    {
        /// <summary>
        /// A URL to the small version (28px x 28px) of the image.
        /// </summary>
        [JsonPropertyName("url_1x")]
        public Uri? Url1x { get; init; }

        /// <summary>
        /// A URL to the medium version (56px x 56px) of the image.
        /// </summary>
        [JsonPropertyName("url_2x")]
        public Uri? Url2x { get; init; }

        /// <summary>
        /// A URL to the large version (112px x 112px) of the image.
        /// </summary>
        [JsonPropertyName("url_4x")]
        public Uri? Url4x { get; init; }
    }
}
