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

namespace StreamActions.Twitch.Api.Common;

/// <summary>
/// Represents the event args for a <see cref="TwitchApi.OnTokenRefreshed"/> event.
/// </summary>
/// <remarks>
/// Constructor.
/// </remarks>
/// <param name="session">The new <see cref="TwitchSession"/> that contains the newly refreshed token.</param>
public sealed class TokenRefreshedEventArgs(TwitchSession session) : EventArgs
{
    #region Public Properties

    /// <summary>
    /// The new <see cref="TwitchSession"/> that contains the newly refreshed token.
    /// </summary>
    public TwitchSession Session { get; init; } = session;

    #endregion Public Properties
}
