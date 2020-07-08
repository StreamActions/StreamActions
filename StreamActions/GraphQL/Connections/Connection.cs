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
using System.Linq;

namespace StreamActions.GraphQL.Connections
{
    /// <summary>
    /// Represents a cursor connection.
    /// </summary>
    /// <typeparam name="TEdgeType">The object type contained by each edge. Must implement <see cref="ICursorable"/>.</typeparam>
    public class Connection<TEdgeType> where TEdgeType : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// Contains the edges returned by the query.
        /// </summary>
        public List<ConnectionEdge<TEdgeType>> Edges => this._edges;

        /// <summary>
        /// Contains the page info about this query.
        /// </summary>
        public ConnectionPageInfo PageInfo { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Generates the <see cref="PageInfo"/> from the current <see cref="Edges"/>.
        /// </summary>
        /// <param name="hasPreviousPage">Indicates if a previous page is available. If paginating with <c>first/after</c>, can be <c>false</c> if not practical to accurately determine this.</param>
        /// <param name="hasNextPage">Indicates if a next page is available. If paginating with <c>last/before</c>, can be <c>false</c> if not practical to accurately determine this.</param>
        public void GeneratePageInfo(bool hasPreviousPage, bool hasNextPage) => this.PageInfo = new ConnectionPageInfo
        {
            EndCursor = this._edges.Last().Cursor,
            HasNextPage = hasNextPage,
            HasPreviousPage = hasPreviousPage,
            StartCursor = this._edges.First().Cursor
        };

        /// <summary>
        /// Inserts a list of <see cref="TEdgeType"/> into <see cref="Edges"/>, converting to a <see cref="ConnectionEdge{TEdgeType}"/>.
        /// If using <c>first/after</c>, the edge closest to <c>after</c> must be the first result.
        /// If using <c>last/before</c>, the edge closest to <c>before</c> must be the last result.
        /// Use <paramref name="reverse"/>, if necessary, to ensure the results follow this rule.
        /// </summary>
        /// <param name="edges">A list of <see cref="TEdgeType"/> to insert.</param>
        /// <param name="reverse">Set to <c>true</c> to reverse the order of <see cref="Edges"/> after inserting.</param>
        public void InsertEdges(List<TEdgeType> edges, bool reverse = false)
        {
            if (edges is null)
            {
                throw new ArgumentNullException(nameof(edges));
            }

            foreach (TEdgeType edge in edges)
            {
                this._edges.Add(new ConnectionEdge<TEdgeType>
                {
                    Cursor = edge.GetCursor(),
                    Node = edge
                });
            }

            if (reverse)
            {
                this._edges.Reverse();
            }
        }

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Field that backs <see cref="Edges"/>.
        /// </summary>
        private readonly List<ConnectionEdge<TEdgeType>> _edges = new List<ConnectionEdge<TEdgeType>>();

        #endregion Private Fields
    }
}