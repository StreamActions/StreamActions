/*
 * Copyright © 2019-2020 StreamActions Team
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

using System;
using System.Collections.Generic;
using System.Security.Principal;

namespace StreamActions.Http.Identity
{
    /// <summary>
    /// An identity claim proven using the HTTP Authorization Basic scheme.
    /// </summary>
    internal class HttpServerBasicIdentity : GenericIdentity
    {
        #region Public Properties

        public override bool IsAuthenticated => this._isAuthenticated;

        #endregion Public Properties

        #region Internal Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="username">The username provided by the client.</param>
        /// <param name="password">The password provided by the client.</param>
        internal HttpServerBasicIdentity(string username, string password) : base(username, "Basic") => this.Password = password;

        #endregion Internal Constructors

        #region Internal Properties

        /// <summary>
        /// The password provided by the client.
        /// </summary>
        internal string Password { get; }

        #endregion Internal Properties

        #region Internal Methods

        /// <summary>
        /// Authenticates this identity.
        /// </summary>
        /// <param name="validIdentities">A Dictionary of valid identities that would result in successful authentication. Key is username; value is password.</param>
        internal void Authenticate(Dictionary<string, string> validIdentities) => this._isAuthenticated = validIdentities.ContainsKey(this.Name) && validIdentities[this.Name].Equals(this.Password, StringComparison.Ordinal);

        /// <summary>
        /// Authenticates this identity.
        /// </summary>
        /// <param name="authenticator">A Func that accepts the username and password provided by the client and returns a <c>bool</c> indicating if authentication is successful.</param>
        internal void Authenticate(Func<string, string, bool> authenticator) => this._isAuthenticated = authenticator.Invoke(this.Name, this.Password);

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Field that backs <see cref="IsAuthenticated"/>.
        /// </summary>
        private bool _isAuthenticated = false;

        #endregion Private Fields
    }
}