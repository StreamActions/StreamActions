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

namespace StreamActions.TwitchAPI
{
    /// <summary>
    /// Handles validation of OAuth tokens and performs HTTP calls for the API.
    /// </summary>
    public class TwitchAPI
    {
        #region Public Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="clientId">A valid Twitch App Client ID.</param>
        /// <param name="oauth">A valid OAuth token made with via the <paramref name="clientId"/>.</param>
        public TwitchAPI(string clientId, string oauth)
        {
            this._clientId = clientId;
            this._oauth = oauth;
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// The current list of scopes for the active OAuth token.
        /// </summary>
        public IReadOnlyList<string> Scopes => this._scopes.AsReadOnly();

        #endregion Public Properties

        #region Internal Enums

        /// <summary>
        /// HTTP Request Methods.
        /// </summary>
        internal enum Request_Method
        {
            /// <summary>
            /// HTTP GET.
            /// </summary>
            GET,

            /// <summary>
            /// HTTP POST.
            /// </summary>
            POST,

            /// <summary>
            /// HTTP PUT.
            /// </summary>
            PUT,

            /// <summary>
            /// HTTP DELETE.
            /// </summary>
            DELETE
        }

        #endregion Internal Enums

        #region Private Fields

        /// <summary>
        /// Helix base URL.
        /// </summary>
        private static readonly string _helix_base_url = "https://api.twitch.tv/helix/";

        /// <summary>
        /// User agent for requests.
        /// </summary>
        private static readonly string _user_agent = "StreamActions/TwitchAPI/1.0";

        /// <summary>
        /// Token validation URL.
        /// </summary>
        private static readonly string _validate_url = "https://id.twitch.tv/oauth2/validate";

        /// <summary>
        /// ClientID for TwitchAPI.
        /// </summary>
        private readonly string _clientId;

        /// <summary>
        /// OAuth for TwitchAPI.
        /// </summary>
        private readonly string _oauth;

        /// <summary>
        /// Backer for <see cref="Scopes"/>.
        /// </summary>
        private readonly List<string> _scopes = new List<string>();

        #endregion Private Fields
    }
}
