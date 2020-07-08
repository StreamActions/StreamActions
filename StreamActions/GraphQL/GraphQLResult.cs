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

using EntityGraphQL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StreamActions.GraphQL
{
    /// <summary>
    /// Contains the results of a GraphQL query or mutation.
    /// </summary>
    public class GraphQLResult : QueryResult
    {
        #region Public Properties

        /// <summary>
        /// The return data.
        /// </summary>
        [JsonPropertyName("data")]
        public new ConcurrentDictionary<string, object> Data => base.Data;

        /// <summary>
        /// Any errors that occurred.
        /// </summary>
        [JsonPropertyName("errors")]
        public new List<GraphQLError> Errors => base.Errors;

        #endregion Public Properties

        #region Public Constructors

        /// <summary>
        /// Constructor. Copies the input object to the base.
        /// </summary>
        /// <param name="result">The input object to copy.</param>
        public GraphQLResult(QueryResult result) : base()
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            foreach (KeyValuePair<string, object> kvp in result.Data)
            {
                _ = this.Data.TryAdd(kvp.Key, kvp.Value);
            }

            foreach (GraphQLError error in result.Errors)
            {
                this.Errors.Add(error);
            }
        }

        #endregion Public Constructors
    }
}