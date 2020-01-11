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
            if (result == null)
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