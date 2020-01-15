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
    /// Contains the PageInfo for a cursor connection.
    /// </summary>
    public class ConnectionPageInfo
    {
        #region Public Properties

        /// <summary>
        /// The cursor to the last edge on the current page.
        /// </summary>
        public string EndCursor { get; set; } = "";

        /// <summary>
        /// Indicates if another page is available after the current page.
        /// </summary>
        public bool HasNextPage { get; set; } = false;

        /// <summary>
        /// Indicates if another page is available before the current page.
        /// </summary>
        public bool HasPreviousPage { get; set; } = false;

        /// <summary>
        /// The cursor to the first edge on the current page.
        /// </summary>
        public string StartCursor { get; set; } = "";

        #endregion Public Properties
    }
}