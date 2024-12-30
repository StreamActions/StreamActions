/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;

namespace StreamActions.Security.OAuth.Twitch
{
    public class TwitchAuthenticationHandler : AspNet.Security.OAuth.Twitch.TwitchAuthenticationHandler
    {
        #region Public Constructors

        public TwitchAuthenticationHandler(
            [NotNull] IOptionsMonitor<TwitchAuthenticationOptions> options,
            [NotNull] ILoggerFactory logger,
            [NotNull] UrlEncoder encoder,
            [NotNull] ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        #endregion Public Constructors

        #region Protected Methods

        protected override string FormatScope()
            => this.FormatScope(((TwitchAuthenticationOptions)this.Options).Scope);

        #endregion Protected Methods
    }
}
