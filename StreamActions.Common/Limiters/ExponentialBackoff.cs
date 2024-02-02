/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Common.Limiters;

/// <summary>
/// Handles backoff timing using an exponentially increasing duration strategy.
/// </summary>
public sealed class ExponentialBackoff : BackoffBase
{
    #region Public Constructors

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="initialDuration">The initial duration of the backoff, after a reset.</param>
    /// <param name="maxDuration">The maximum allowed backoff duration.</param>
    public ExponentialBackoff(TimeSpan initialDuration, TimeSpan maxDuration) : base(initialDuration, maxDuration) { }

    #endregion Public Constructors

    #region Protected Methods

    /// <summary>
    /// Calculates the next value for <see cref="NextDuration"/>, in ticks.
    /// </summary>
    /// <returns>The next value that will be set for <see cref="NextDuration"/>, in ticks.</returns>
    protected override long CalcNextDurationTicks() => this.NextDuration.Ticks * 2;

    #endregion Protected Methods
}
