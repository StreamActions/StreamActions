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