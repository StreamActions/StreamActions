/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Common.Attributes;

/// <summary>
/// Indicates the URI, issue, and diff parser associated with a <see cref="Interfaces.IComponent"/>, allowing a GitHub Actions job to automatically detect when the source documentation has changed.
/// </summary>
/// <param name="friendlyName">The friendly name of the documentation, to be used when updating issues programmatically.</param>
/// <param name="issueId">The GitHub issue number for tracking the API.</param>
/// <param name="uri">The URI of the source documentation for this component.</param>
/// <param name="parser">The parser to use. See <c>Diff/parsers</c>.</param>
///
///
///
/// <remarks>
/// <para>
/// RegEx (Python3): <c><![CDATA[r"(?s)\[DocParser\(\s*\"(?P<friendlyname>[^\"]+)\"\s*,\s*(?P<issue>[0-9]+)\s*,\s*\"(?P<url>[^\"]+)\"\s*,\s*\"(?P<parser>[^\"]+)\"\s*\)\]"]]></c>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DocParserAttribute(string friendlyName, int issueId, string uri, string parser) : Attribute
{
    #region Public Properties

    /// <summary>
    /// The friendly name of the documentation, to be used when updating issues programmatically.
    /// </summary>
    public string FriendlyName { get; init; } = friendlyName;

    /// <summary>
    /// The GitHub issue number for tracking the API.
    /// </summary>
    public int IssueId { get; init; } = issueId;

    /// <summary>
    /// The parser module to use
    /// </summary>
    public string Parser { get; init; } = parser;

    /// <summary>
    /// The URI of the source documentation for this component.
    /// </summary>
    public string Uri { get; init; } = uri;

    #endregion Public Properties
}
