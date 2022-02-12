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

using System.Text;

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
        public static Uri BuildUri(Uri baseUri, IEnumerable<KeyValuePair<string, IEnumerable<string>>>? queryParams = null, IEnumerable<KeyValuePair<string, IEnumerable<string>>>? fragmentParams = null)
        {
            if (baseUri is null)
            {
                throw new ArgumentNullException(nameof(baseUri));
            }

            StringBuilder relativeUri = new();

            if (queryParams is not null && queryParams.Any())
            {
                _ = relativeUri.Append('?');

                bool first = true;
                foreach (KeyValuePair<string, IEnumerable<string>> kvp in queryParams)
                {
                    if (!first)
                    {
                        _ = relativeUri.Append('&');
                    }
                    else
                    {
                        first = false;
                    }

                    foreach (string value in kvp.Value)
                    {
                        _ = relativeUri.Append(Uri.EscapeDataString(kvp.Key)).Append('=').Append(Uri.EscapeDataString(value));
                    }
                }
            }

            if (fragmentParams is not null && fragmentParams.Any())
            {
                _ = relativeUri.Append('#');

                bool first = true;
                foreach (KeyValuePair<string, IEnumerable<string>> kvp in fragmentParams)
                {
                    if (!first)
                    {
                        _ = relativeUri.Append('&');
                    }
                    else
                    {
                        first = false;
                    }

                    foreach (string value in kvp.Value)
                    {
                        _ = relativeUri.Append(Uri.EscapeDataString(kvp.Key)).Append('=').Append(Uri.EscapeDataString(value));
                    }
                }
            }

            return new Uri(baseUri, relativeUri.ToString());
        }

        /// <summary>
        /// Converts the <see cref="DateTime"/> to an RFC3339 representation of its value.
        /// </summary>
        /// <param name="dt">The <see cref="DateTime"/> to convert.</param>
        /// <returns>The value of <paramref name="dt"/> as an RFC3339-formatted string.</returns>
        public static string ToRfc3339(this DateTime dt) => dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        #endregion Public Methods
    }
}
