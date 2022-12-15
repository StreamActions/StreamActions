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

namespace StreamActions.Common.Exceptions;

/// <summary>
/// The exception that is thrown when a required OAuth scope is missing.
/// </summary>
public sealed class ScopeMissingException : InvalidOperationException
{
    #region Public Constructors

    /// <summary>
    /// Default Constructor.
    /// </summary>
    public ScopeMissingException()
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
