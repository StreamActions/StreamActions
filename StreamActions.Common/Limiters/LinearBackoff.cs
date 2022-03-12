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

namespace StreamActions.Common.Limiters
{
    /// <summary>
    /// Handles backoff timing using an linearly increasing duration strategy.
    /// </summary>
    public sealed class LinearBackoff : BackoffBase
    {
        #region Public Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initialDuration">The initial duration of the backoff, after a reset.</param>
        /// <param name="maxDuration">The maximum allowed backoff duration.</param>
        /// <param name="stepSize">The duration to add to the previous <see cref="BackoffBase.NextDuration"/> to calculate the next backoff duration.</param>
        public LinearBackoff(TimeSpan initialDuration, TimeSpan maxDuration, TimeSpan stepSize) : base(initialDuration, maxDuration) => this._stepSize = stepSize;

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// The duration added to the previous <see cref="BackoffBase.NextDuration"/> to generate the next backoff duration.
        /// </summary>
        public TimeSpan StepSize => TimeSpan.FromTicks(this._stepSize.Ticks);

        #endregion Public Properties

        #region Protected Methods

        /// <summary>
        /// Calculates the next value for <see cref="NextDuration"/>, in ticks.
        /// </summary>
        /// <returns>The next value that will be set for <see cref="NextDuration"/>, in ticks.</returns>
        protected override long CalcNextDurationTicks() => this.NextDuration.Ticks + this._stepSize.Ticks;

        #endregion Protected Methods

        #region Private Fields

        /// <summary>
        /// The duration added to the previous <see cref="BackoffBase.NextDuration"/> to generate the next backoff duration.
        /// </summary>
        private readonly TimeSpan _stepSize;

        #endregion Private Fields
    }
}
