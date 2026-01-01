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

using StreamActions.Common.Exceptions;
using StreamActions.Twitch.OAuth;

namespace StreamActions.Twitch.Exceptions;

/// <summary>
/// The exception that is thrown when a required OAuth scope from Twitch is missing.
/// </summary>
public sealed class TwitchScopeMissingException : ScopeMissingException
{

    #region Public Constructors

    /// <summary>
    /// Default Constructor.
    /// </summary>
    public TwitchScopeMissingException()
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="scope">The scope that is missing from the OAuth token.</param>
    public TwitchScopeMissingException(string? scope) : base(scope)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="scope">The scope that is missing from the OAuth token.</param>
    public TwitchScopeMissingException(Scope? scope) : base(scope?.Name)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="scopes">The scopes that are missing from the OAuth token.</param>
    public TwitchScopeMissingException(params string?[] scopes) : base(scopes)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="scopes">The scopes that are missing from the OAuth token.</param>
    public TwitchScopeMissingException(params Scope?[] scopes) : base(scopes?.Select(scope => scope?.Name).ToArray() ?? [])
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="scope">The scope that is missing from the OAuth token.</param>
    /// <param name="innerException">The inner exception that caused the current exception.</param>
    public TwitchScopeMissingException(string? scope, Exception? innerException) : base(scope, innerException)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="scope">The scope that is missing from the OAuth token.</param>
    /// <param name="innerException">The inner exception that caused the current exception.</param>
    public TwitchScopeMissingException(Scope? scope, Exception? innerException) : base(scope?.Name, innerException)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="scopes">The scopes that are missing from the OAuth token.</param>
    /// <param name="innerException">The inner exception that caused the current exception.</param>
    public TwitchScopeMissingException(string?[] scopes, Exception? innerException) : base(scopes, innerException)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="scopes">The scopes that are missing from the OAuth token.</param>
    /// <param name="innerException">The inner exception that caused the current exception.</param>
    public TwitchScopeMissingException(Scope?[] scopes, Exception? innerException) : base(scopes?.Select(scope => scope?.Name).ToArray() ?? [], innerException)
    {
    }

    #endregion Public Constructors

}
