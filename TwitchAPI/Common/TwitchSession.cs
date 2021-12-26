/*
 * Copyright © 2019-2021 StreamActions Team
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

using StreamActions.Util;

namespace StreamActions.TwitchAPI.Common
{
    /// <summary>
    /// Contains parameters for a Twitch API session.
    /// </summary>
    public record TwitchSession
    {
        /// <summary>
        /// Rate Limiter for TwitchAPI.
        /// </summary>
        public TokenBucketRateLimiter RateLimiter { get; init; } = new(1, 60);

        /// <summary>
        /// The OAuth token.
        /// </summary>
        public string? OAuth
        {
            get => this._oauth;
            init => this._oauth = string.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(value)) : value;
        }

        /// <summary>
        /// Backer for <see cref="OAuth"/>.
        /// </summary>
        private string? _oauth;
    }
}
