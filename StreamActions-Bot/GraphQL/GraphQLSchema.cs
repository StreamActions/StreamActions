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

using StreamActions.Database.Documents;

namespace StreamActions.GraphQL
{
    /// <summary>
    /// A GraphQL schema object.
    /// </summary>
    public class GraphQLSchema
    {
        #region Public Properties

        /// <summary>
        /// A <see cref="UserDocument"/> indicating the user that this client is authenticated as.
        /// </summary>
        public UserDocument AuthenticatedUser => this._authenticatedUser;

        #endregion Public Properties

        #region Internal Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="authenticatedUser">The <see cref="UserDocument"/> indicating the user that this client is authenticated as.</param>
        internal GraphQLSchema(UserDocument authenticatedUser) => this._authenticatedUser = authenticatedUser;

        #endregion Internal Constructors

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="AuthenticatedUser"/> property.
        /// </summary>
        private readonly UserDocument _authenticatedUser;

        #endregion Private Fields
    }
}