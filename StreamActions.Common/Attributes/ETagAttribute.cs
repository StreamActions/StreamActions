/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Common.Attributes;

/// <summary>
/// Indicates the URI and ETag associated with a <see cref="Interfaces.IComponent"/>, allowing a diff parser to automatically detect when the source documentation has changed.
/// </summary>
/// <param name="friendlyName">The friendly name of the documentation, to be used when updating issues programmatically.</param>
/// <param name="issueId">The issue number for tracking the API.</param>
/// <param name="uri">The URI of the source documentation for this component.</param>
/// <param name="eTag">The ETag of the latest version of the documentation that this component conforms to. This is usually a hash of the output from <paramref name="parser"/>.</param>
/// <param name="timestamp">The timestamp when the <paramref name="eTag"/> was updated.</param>
/// <param name="parser">The parser to use. See <c>Diff/parsers</c>.</param>
/// <param name="parameters">Parameters for <paramref name="parser"/>.</param>
/// <remarks>
/// <para>
/// Main RegEx (Python3): <code>r"(?s)\[ETag\(\s*\"(?P<friendlyname>[^\"]+)\"\s*,\s*(?P<issue>[0-9]+)\s*,\s*\"(?P<url>[^\"]+)\"\s*,\s*\"(?P<hash>[^\"]+)\"\s*,\s*\"(?P<date>[^\"]+)\"\s*,\s*\"(?P<parser>[^\"]+)\"\s*,\s*[{\[](?P<parameters>.*?)[}\]]\s*\)\]"</code>
/// </para>
/// <para>
/// Array RegEx (Python3): <code>r"(?s)(\"(?P<param>([^\"]|\\\")+)\"(,|$))"</code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ETagAttribute(string friendlyName, int issueId, string uri, string eTag, string timestamp, string parser, string[] parameters) : Attribute
{
    #region Public Properties

    /// <summary>
    /// The ETag of the latest version of the documentation that this component conforms to.
    /// </summary>
    public string ETag { get; init; } = eTag;

    /// <summary>
    /// The friendly name of the documentation, to be used when updating issues programmatically.
    /// </summary>
    public string FriendlyName { get; init; } = friendlyName;

    /// <summary>
    /// The issue number for tracking the API.
    /// </summary>
    public int IssueId { get; init; } = issueId;

    /// <summary>
    /// Additional parameters for <see cref="Parser"/>.
    /// </summary>
    public string[] Parameters { get; init; } = parameters;

    /// <summary>
    /// The parser module to use
    /// </summary>
    public string Parser { get; init; } = parser;

    /// <summary>
    /// The timestamp when the <see cref="ETag"/> was updated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Parse(timestamp, CultureInfo.InvariantCulture);

    /// <summary>
    /// The URI of the source documentation for this component.
    /// </summary>
    public string Uri { get; init; } = uri;

    #endregion Public Properties
}
