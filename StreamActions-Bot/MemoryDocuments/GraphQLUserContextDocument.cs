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
using StreamActions.GraphQL;

namespace StreamActions.MemoryDocuments
{
    /// <summary>
    /// Contains the user context for a GraphQL WebSocket connection
    /// </summary>
    public class GraphQLUserContextDocument
    {
        #region Public Properties

        /// <summary>
        /// The current schema object assigned to the user, with query permissions applied.
        /// </summary>
        public GraphQLSchema Schema { get; internal set; }

        /// <summary>
        /// The UserDocument the client has authenticated as.
        /// </summary>
        public UserDocument User { get; internal set; }

        #endregion Public Properties
    }
}