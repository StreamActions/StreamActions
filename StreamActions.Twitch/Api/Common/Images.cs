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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("url_1x")]
        public string? Url1xString { get; init; }

        /// <summary>
        /// <see cref="Url1xString"/> as a <see cref="Uri"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? Url1x => this.Url1xString is not null ? new(this.Url1xString) : null;

        /// <summary>
        /// A URL to the medium version (56px x 56px) of the image.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("url_2x")]
        public string? Url2xString { get; init; }

        /// <summary>
        /// <see cref="Url2xString"/> as a <see cref="Uri"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? Url2x => this.Url2xString is not null ? new(this.Url2xString) : null;

        /// <summary>
        /// A URL to the large version (112px x 112px) of the image.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("url_4x")]
        public string? Url4xString { get; init; }

        /// <summary>
        /// <see cref="Url4xString"/> as a <see cref="Uri"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? Url4x => this.Url4xString is not null ? new(this.Url4xString) : null;
    }
}
