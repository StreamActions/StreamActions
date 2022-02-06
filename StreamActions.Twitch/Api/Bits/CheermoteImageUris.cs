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
        /// The uri to the 1x size image as a string.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("1")]
        public string? Uri1xString { get; init; }

        /// <summary>
        /// <see cref="Uri1xString"/> as a <see cref="Uri"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? Uri1x => this.Uri1xString is not null ? new(this.Uri1xString) : null;

        /// <summary>
        /// The uri to the 1.5x size image as a string.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("1.5")]
        public string? Uri1p5xString { get; init; }

        /// <summary>
        /// <see cref="Uri1p5xString"/> as a <see cref="Uri"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? Uri1p5x => this.Uri1p5xString is not null ? new(this.Uri1p5xString) : null;

        /// <summary>
        /// The uri to the 2x size image as a string.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("2")]
        public string? Uri2xString { get; init; }

        /// <summary>
        /// <see cref="Uri2xString"/> as a <see cref="Uri"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? Uri2x => this.Uri2xString is not null ? new(this.Uri2xString) : null;

        /// <summary>
        /// The uri to the 3x size image as a string.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("3")]
        public string? Uri3xString { get; init; }

        /// <summary>
        /// <see cref="Uri3xString"/> as a <see cref="Uri"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? Uri3x => this.Uri3xString is not null ? new(this.Uri3xString) : null;

        /// <summary>
        /// The uri to the 4x size image as a string.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("4")]
        public string? Uri4xString { get; init; }

        /// <summary>
        /// <see cref="Uri4xString"/> as a <see cref="Uri"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public Uri? Uri4x => this.Uri4xString is not null ? new(this.Uri4xString) : null;
    }
}
