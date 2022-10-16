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

namespace StreamActions.Common.Attributes;

/// <summary>
/// Indicates the URI and ETag associated with a <see cref="StreamActions.Common.Interfaces.Component"/>, allowing <c>Diff.py</c> to automatically detect when the source documentation has changed.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ETagAttribute : Attribute
{
    #region Public Constructors

    /// <summary>
    /// Attribute Constructor.
    /// </summary>
    /// <param name="uri">The URI of the source documentation for this component.</param>
    /// <param name="eTag">The ETag of the latest version of the documentation that this component conforms to.</param>
    /// <param name="differParameters">Parameters for <c>Diff.py</c>.</param>
    public ETagAttribute(string uri, string eTag, string[] differParameters)
    {
        this.Uri = uri;
        this.ETag = eTag;
        this.DifferParameters = differParameters.ToList();
    }

    #endregion Public Constructors

    #region Public Properties

    /// <summary>
    /// Parameters for <c>Diff.py</c>.
    /// </summary>
    public IReadOnlyCollection<string> DifferParameters { get; init; }

    /// <summary>
    /// The ETag of the latest version of the documentation that this component conforms to.
    /// </summary>
    public string ETag { get; init; }

    /// <summary>
    /// The URI of the source documentation for this component.
    /// </summary>
    public string Uri { get; init; }

    #endregion Public Properties
}
