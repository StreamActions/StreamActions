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
    /// Represents the product acquired, as it looked at the time of the <see cref="ExtensionTransaction"/>.
    /// </summary>
    public record ExtensionProductData
    {
        /// <summary>
        /// Set to twitch.ext + your Extension ID.
        /// </summary>
        [JsonPropertyName("domain")]
        public string? Domain { get; init; }

        /// <summary>
        /// Unique identifier for the product across the Extension
        /// </summary>
        [JsonPropertyName("sku")]
        public string? Sku { get; init; }

        /// <summary>
        /// A <see cref="ExtensionProductCost"/> representing the cost to acquire the product.
        /// </summary>
        [JsonPropertyName("cost")]
        public ExtensionProductCost? Cost { get; init; }

        /// <summary>
        /// Indicates if the product is in development
        /// </summary>
        [JsonPropertyName("inDevelopment")]
        public bool? InDevelopment { get; init; }

        /// <summary>
        /// Display name of the product.
        /// </summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        /// <summary>
        /// The expiration timestamp for the product. Currently always empty since only unexpired products can be purchased.
        /// </summary>
        [JsonPropertyName("expiration")]
        public DateTime? Expiration { get; init; }

        /// <summary>
        /// Indicates whether or not the data was sent over the Extension PubSub to all instances of the Extension.
        /// </summary>
        [JsonPropertyName("broadcast")]
        public bool? Broadcast { get; init; }
    }
}
