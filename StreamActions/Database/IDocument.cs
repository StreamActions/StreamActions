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

namespace StreamActions.Database
{
    /// <summary>
    /// An interface for a top-level Document that may require initialization in the database.
    /// </summary>
    public interface IDocument
    {
        #region Public Methods

        /// <summary>
        /// Initializes the Collection for the Document and/or sets up the indexes, if necessary.
        /// </summary>
        public void Initialize();

        #endregion Public Methods
    }
}