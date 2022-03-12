/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
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
