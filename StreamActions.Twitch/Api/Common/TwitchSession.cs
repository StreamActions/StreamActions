/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using StreamActions.Common.Limiters;

namespace StreamActions.Twitch.Api.Common
{
    /// <summary>
    /// Contains parameters for a Twitch API session.
    /// </summary>
    public record TwitchSession
    {
        /// <summary>
        /// Rate Limiter for TwitchAPI.
        /// </summary>
        public TokenBucketRateLimiter RateLimiter { get; init; } = new(1, TimeSpan.FromSeconds(60));

        /// <summary>
        /// The OAuth token data.
        /// </summary>
        /// <exception cref="ArgumentNullException">Attempt to set to null.</exception>
        public TwitchToken? Token
        {
            get => this._token;
            internal set => this._token = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Used as a locking object by <see cref="TwitchApi.PerformHttpRequest(HttpMethod, Uri, TwitchSession, HttpContent?, bool)"/> to prevent multiple simultaneous or back-to-back OAuth Refreshes.
        /// </summary>
        internal Mutex _refreshLock = new();

        /// <summary>
        /// Backer for <see cref="Token"/>.
        /// </summary>
        private TwitchToken? _token;
    }
}
