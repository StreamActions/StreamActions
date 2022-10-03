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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Common
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

        /// <summary>
        /// The scopes that are authorized under the OAuth token.
        /// </summary>
        public IReadOnlyList<string>? Scopes { get; init; }

        /// <summary>
        /// The login name associated with the OAuth token.
        /// </summary>
        public string? Login { get; init; }

        /// <summary>
        /// The User Id associated with the OAuth token.
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// Checks if the specified scope is present in <see cref="Scopes"/>.
        /// </summary>
        /// <param name="scope">The scope to find.</param>
        /// <returns><see langword="true"/> if <see cref="Scopes"/> is null or contains <paramref name="scope"/>.</returns>
        public bool HasScope(string scope) => this.Scopes is null || this.Scopes.Contains(scope);
    }
}
