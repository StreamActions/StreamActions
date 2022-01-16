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

namespace StreamActions.Common
{
    /// <summary>
    /// Utility Members.
    /// </summary>
    public static class Util
    {
        #region Public Methods

        /// <summary>
        /// Builds a <see cref="Uri"/>, escaping query and fragment parameters.
        /// </summary>
        /// <param name="baseUri">The base uri.</param>
        /// <param name="queryParams">The query parameters.</param>
        /// <param name="fragmentParams">The fragment parameters.</param>
        /// <returns>A <see cref="Uri"/> with all query and fragment parameters escaped and appended.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="baseUri"/> is null.</exception>
        public static Uri BuildUri(Uri baseUri, IEnumerable<KeyValuePair<string, string>>? queryParams = null, IEnumerable<KeyValuePair<string, string>>? fragmentParams = null)
        {
            if (baseUri is null)
            {
                throw new ArgumentNullException(nameof(baseUri));
            }

            string relativeUri = "";

            if (queryParams is not null && queryParams.Any())
            {
                if (!relativeUri.Contains('?', StringComparison.InvariantCulture))
                {
                    relativeUri += "?";
                }

                foreach (KeyValuePair<string, string> kvp in queryParams)
                {
                    if (!relativeUri.EndsWith("?", StringComparison.InvariantCulture))
                    {
                        relativeUri += "&";
                    }

                    relativeUri += Uri.EscapeDataString(kvp.Key) + "=" + Uri.EscapeDataString(kvp.Value);
                }
            }

            if (fragmentParams is not null && fragmentParams.Any())
            {
                if (!relativeUri.Contains('#', StringComparison.InvariantCulture))
                {
                    relativeUri += "#";
                }

                foreach (KeyValuePair<string, string> kvp in fragmentParams)
                {
                    if (!relativeUri.EndsWith("#", StringComparison.InvariantCulture))
                    {
                        relativeUri += "&";
                    }

                    relativeUri += Uri.EscapeDataString(kvp.Key) + "=" + Uri.EscapeDataString(kvp.Value);
                }
            }

            return new Uri(baseUri, relativeUri);
        }

        #endregion Public Methods
    }
}
