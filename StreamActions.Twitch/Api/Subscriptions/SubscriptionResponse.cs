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

using StreamActions.Twitch.Api.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Subscriptions
{
    /// <summary>
    /// A <see cref="ResponseData{TDataType}"/> with the subscription <see cref="Points"/> field.
    /// </summary>
    public record SubscriptionResponse : ResponseData<Subscription>
    {
        /// <summary>
        /// The current number of subscriber points earned by this broadcaster.
        /// </summary>
        /// <remarks>
        /// Points are based on the subscription tier of each user that subscribes to this broadcaster.
        /// For example, a Tier 1 subscription is worth 1 point, Tier 2 is worth 2 points, and Tier 3 is worth 6 points.
        /// The number of points determines the number of emote slots that are unlocked for the broadcaster.
        /// </remarks>
        [JsonPropertyName("points")]
        public int? Points { get; init; }
    }
}
