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

namespace StreamActions.GraphQL.Connections
{
    /// <summary>
    /// Represents an edge in a cursor connection.
    /// </summary>
    /// <typeparam name="TEdgeType">The object type of the <see cref="Node"/>. Must implement <see cref="ICursorable"/>.</typeparam>
    public class ConnectionEdge<TEdgeType> where TEdgeType : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// The cursor to this edge.
        /// </summary>
        public string Cursor { get; set; }

        /// <summary>
        /// The object represented by this edge.
        /// </summary>
        public TEdgeType Node { get; set; }

        #endregion Public Properties
    }
}