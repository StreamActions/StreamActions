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

namespace StreamActions.Twitch.API.Common
{
    public record TwitchToken
    {
        /// <summary>
        /// The OAuth token.
        /// </summary>
        /// <exception cref="ArgumentNullException">Attempt to set to null or whitespace.</exception>
        public string? OAuth
        {
            get => this._oauth;
            init => this._oauth = string.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(value)) : value;
        }

        /// <summary>
        /// Backer for <see cref="OAuth"/>.
        /// </summary>
        private string? _oauth;

        /// <summary>
        /// The refresh token.
        /// </summary>
        public string? Refresh { get; init; }

        /// <summary>
        /// The expiration timestamp of the <see cref="OAuth"/>.
        /// </summary>
        public DateTime? Expires { get; init; }
    }
}
