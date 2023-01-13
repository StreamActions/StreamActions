/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StreamActions.Common;

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

                if (kvp.Value.Any())
                {
                    foreach (string value in kvp.Value)
                    {
                        _ = relativeUri.Append(Uri.EscapeDataString(kvp.Key)).Append('=').Append(Uri.EscapeDataString(value));
                    }
                }
                else
                {
                    _ = relativeUri.Append(Uri.EscapeDataString(kvp.Key));
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

                if (kvp.Value.Any())
                {
                    foreach (string value in kvp.Value)
                    {
                        _ = relativeUri.Append(Uri.EscapeDataString(kvp.Key)).Append('=').Append(Uri.EscapeDataString(value));
                    }
                }
                else
                {
                    _ = relativeUri.Append(Uri.EscapeDataString(kvp.Key));
                }
            }
        }

        return new Uri(baseUri, relativeUri.ToString());
    }

    /// <summary>
    /// Converts a duration string in the format <c>1w2d3h4m5s</c> into a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="duration">A duration string in the format <c>1w2d3h4m5s</c>. 0-valued segments can be excluded.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the time passed in.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An unknown capture group was returned by <see cref="_durationRegex"/>.</exception>
    public static TimeSpan DurationStringToTimeSpan(string duration)
    {
        TimeSpan t = new(0);
        Match m = _durationRegex.Match(duration);

        foreach (Group g in m.Groups)
        {
            t = g.Name switch
            {
                "weeks" => t.Add(TimeSpan.FromDays(7 * int.Parse(g.Value, CultureInfo.InvariantCulture))),
                "days" => t.Add(TimeSpan.FromDays(int.Parse(g.Value, CultureInfo.InvariantCulture))),
                "hours" => t.Add(TimeSpan.FromHours(int.Parse(g.Value, CultureInfo.InvariantCulture))),
                "minutes" => t.Add(TimeSpan.FromMinutes(int.Parse(g.Value, CultureInfo.InvariantCulture))),
                "seconds" => t.Add(TimeSpan.FromSeconds(int.Parse(g.Value, CultureInfo.InvariantCulture))),
                _ => throw new ArgumentOutOfRangeException(nameof(duration)),
            };
        }

        return t;
    }

    #endregion Public Methods

    #region Private Fields

    /// <summary>
    /// The <see cref="Regex"/> capturing time segments for <see cref="DurationStringToTimeSpan(string)"/>.
    /// </summary>
    private static readonly Regex _durationRegex = new("((?<weeks>[0-9]*)w)?((?<days>[0-9]*)d)?((?<hours>[0-9]*)h)?((?<minutes>[0-9]*)m)?((?<seconds>[0-9]*)s)?");

    #endregion Private Fields
}
