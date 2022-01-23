/*
 * Copyright © 2019-2022 StreamActions Team
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

namespace StreamActions.Common.Exceptions
{
    /// <summary>
    /// The exception that is thrown when a required OAuth scope is missing.
    /// </summary>
    public class ScopeMissingException : InvalidOperationException
    {
        #region Public Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public ScopeMissingException() : base()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="scope">The scope that is missing from the OAuth token.</param>
        public ScopeMissingException(string scope) : base("Missing scope " + scope + ".")
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="scope">The scope that is missing from the OAuth token.</param>
        /// <param name="innerException">The inner exception that caused the current exception.</param>
        public ScopeMissingException(string scope, Exception innerException) : base("Missing scope " + scope + ".", innerException)
        {
        }

        #endregion Public Constructors
    }
}
